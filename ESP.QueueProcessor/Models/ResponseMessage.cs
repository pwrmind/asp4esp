/// <summary>
/// Represents a response message to be sent back to the queue
/// </summary>
public class ResponseMessage
{
    public string RequestId { get; set; }
    public int StatusCode { get; set; }
    public Dictionary<string, string> Headers { get; set; }
    public string Body { get; set; }
} 