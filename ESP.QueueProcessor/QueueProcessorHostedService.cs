using Microsoft.AspNetCore.Http.Features;
using System.Text;

/// <summary>
/// Background service that processes messages from the queue using ASP.NET Core middleware pipeline
/// This service bridges the gap between message queues and ASP.NET Core's request processing pipeline
/// </summary>
public class QueueProcessorHostedService : BackgroundService
{
    private readonly IMessageQueueService _queueService;
    private readonly RequestDelegateHolder _delegateHolder;
    private readonly ILogger<QueueProcessorHostedService> _logger;
    private readonly IHttpContextFactory _httpContextFactory;
    private readonly IServiceProvider _serviceProvider;

    public QueueProcessorHostedService(
        IMessageQueueService queueService,
        RequestDelegateHolder delegateHolder,
        ILogger<QueueProcessorHostedService> logger,
        IHttpContextFactory httpContextFactory,
        IServiceProvider serviceProvider)
    {
        _queueService = queueService;
        _delegateHolder = delegateHolder;
        _logger = logger;
        _httpContextFactory = httpContextFactory;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Main execution loop that continuously processes messages from the queue
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var message = await _queueService.ReceiveMessageAsync(stoppingToken);
            if (message != null)
            {
                await ProcessMessageAsync(message, stoppingToken);
            }
            await Task.Delay(1000, stoppingToken);
        }
    }

    /// <summary>
    /// Processes a single message by creating an HTTP context and running it through the ASP.NET Core pipeline
    /// </summary>
    private async Task ProcessMessageAsync(QueueMessage message, CancellationToken token)
    {
        if (message == null) return;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var httpContext = CreateHttpContext(message);
            httpContext.RequestServices = scope.ServiceProvider;

            await _delegateHolder.RequestDelegate(httpContext);
            await SaveResponseAsync(message, httpContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message {MessageId}", message.Id);
        }
        finally
        {
            await _queueService.DeleteMessageAsync(message.Id, token);
        }
    }

    /// <summary>
    /// Creates an HTTP context from a queue message, simulating a real HTTP request
    /// </summary>
    private HttpContext CreateHttpContext(QueueMessage message)
    {
        // Create feature collection with required components
        var features = new FeatureCollection();

        // Initialize required features
        features.Set<IHttpRequestFeature>(new HttpRequestFeature());
        features.Set<IHttpResponseFeature>(new HttpResponseFeature());
        features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(Stream.Null));

        // Create context through factory
        var httpContext = _httpContextFactory.Create(features);

        // Set required properties
        httpContext.Request.Method = message.Method ?? "GET"; // default value
        httpContext.Request.Path = message.Path ?? "/";
        httpContext.Request.QueryString = new QueryString(message.QueryString);

        // Set headers
        foreach (var header in message.Headers ?? new Dictionary<string, string>())
        {
            httpContext.Request.Headers[header.Key] = header.Value;
        }

        // Process request body
        if (!string.IsNullOrEmpty(message.Body))
        {
            var bodyBytes = Encoding.UTF8.GetBytes(message.Body);
            httpContext.Request.Body = new MemoryStream(bodyBytes);
            httpContext.Request.ContentLength = bodyBytes.Length;
        }

        httpContext.Response.Body = new MemoryStream();
        return httpContext;
    }

    /// <summary>
    /// Saves the response from the HTTP context back to the queue
    /// </summary>
    private async Task SaveResponseAsync(QueueMessage originalMessage, HttpContext httpContext)
    {
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();

        var response = new ResponseMessage
        {
            RequestId = originalMessage.Id,
            StatusCode = httpContext.Response.StatusCode,
            Headers = httpContext.Response.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
            Body = responseBody
        };

        await _queueService.SendResponseAsync(response);
    }
}