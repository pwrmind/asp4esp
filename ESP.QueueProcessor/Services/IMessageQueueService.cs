/// <summary>
/// Interface defining the contract for message queue services.
/// This interface abstracts the communication with different types of message queues (database, message brokers, etc.)
/// </summary>
public interface IMessageQueueService
{
    /// <summary>
    /// Receives a message from the queue asynchronously
    /// </summary>
    /// <param name="ct">Cancellation token for the operation</param>
    /// <returns>A QueueMessage object containing the request details</returns>
    Task<QueueMessage> ReceiveMessageAsync(CancellationToken ct);

    /// <summary>
    /// Sends a response message back to the queue
    /// </summary>
    /// <param name="response">The response message to send</param>
    Task SendResponseAsync(ResponseMessage response);

    /// <summary>
    /// Deletes a processed message from the queue
    /// </summary>
    /// <param name="messageId">ID of the message to delete</param>
    /// <param name="ct">Cancellation token for the operation</param>
    Task DeleteMessageAsync(string messageId, CancellationToken ct);
} 