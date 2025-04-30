/// <summary>
/// Represents a message received from the queue, containing HTTP request details
/// </summary>
public class QueueMessage
{
    public string Id { get; set; }
    public string Method { get; set; }
    public string Path { get; set; }
    public string QueryString { get; set; }
    public Dictionary<string, string> Headers { get; set; }
    public string Body { get; set; }
} 