using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Npgsql;
using NpgsqlTypes;

/// <summary>
/// Implementation of IMessageQueueService using PostgreSQL as the message queue
/// This service provides a reliable way to process messages using database tables
/// </summary>
public class DatabaseQueueService : IMessageQueueService
{
    private readonly string _connectionString;

    public DatabaseQueueService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("PostgreSQL");
    }

    /// <summary>
    /// Receives a message from the database queue using SKIP LOCKED pattern
    /// This ensures that multiple consumers can process messages concurrently
    /// </summary>
    public async Task<QueueMessage> ReceiveMessageAsync(CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        // Use CTE for atomic message retrieval and locking
        var commandText = @"
            WITH locked_message AS (
                SELECT Id
                FROM requests
                WHERE processed = false
                ORDER BY created_at
                FOR UPDATE SKIP LOCKED
                LIMIT 1
            )
            UPDATE requests r
            SET processed = true
            FROM locked_message lm
            WHERE r.Id = lm.Id
            RETURNING r.*;";

        await using var cmd = new NpgsqlCommand(commandText, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (await reader.ReadAsync(ct))
        {
            return new QueueMessage
            {
                Id = reader.GetGuid(0).ToString(),
                Method = reader.GetString(1),
                Path = reader.GetString(2),
                QueryString = reader.IsDBNull(3) ? null : reader.GetString(3),
                Headers = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                    reader.GetString(4)),
                Body = reader.IsDBNull(5) ? null : reader.GetString(5)
            };
        }

        return null;
    }

    /// <summary>
    /// Deletes a processed message from the requests table
    /// </summary>
    public async Task DeleteMessageAsync(string messageId, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var commandText = "DELETE FROM requests WHERE Id = @id";
        await using var cmd = new NpgsqlCommand(commandText, connection);
        cmd.Parameters.AddWithValue("id", Guid.Parse(messageId));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Saves the response to the responses table for later retrieval
    /// </summary>
    public async Task SendResponseAsync(ResponseMessage response)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var commandText = @"
            INSERT INTO responses (RequestId, StatusCode, Headers, Body)
            VALUES (@requestId, @statusCode, @headers::jsonb, @body)";

        await using var cmd = new NpgsqlCommand(commandText, connection);
        cmd.Parameters.AddWithValue("requestId", Guid.Parse(response.RequestId));
        cmd.Parameters.AddWithValue("statusCode", response.StatusCode);
        cmd.Parameters.AddWithValue("headers", NpgsqlDbType.Jsonb,
            JsonConvert.SerializeObject(response.Headers));
        cmd.Parameters.AddWithValue("body", response.Body ?? (object)DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }
}