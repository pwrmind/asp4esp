using Microsoft.AspNetCore.Http;

/// <summary>
/// Holds the ASP.NET Core request delegate that processes incoming messages
/// </summary>
public class RequestDelegateHolder
{
    public RequestDelegate RequestDelegate { get; set; }
} 