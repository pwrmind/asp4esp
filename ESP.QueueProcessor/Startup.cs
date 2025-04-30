public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();
        services.AddHttpContextAccessor(); // Добавляем поддержку HttpContext

        services.AddSingleton<RequestDelegateHolder>();
        services.AddSingleton<IMessageQueueService, DatabaseQueueService>();
        services.AddHostedService<QueueProcessorHostedService>();
        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
    }

    public void Configure(IApplicationBuilder app, IHostApplicationLifetime lifetime)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });

        var holder = app.ApplicationServices.GetRequiredService<RequestDelegateHolder>();
        holder.RequestDelegate = app.Build();
    }
}