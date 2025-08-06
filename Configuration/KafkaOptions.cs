namespace Configuration;

/// <summary>
/// Configuration options for Kafka producer
/// Follows the Options pattern for type-safe configuration
/// </summary>
public class KafkaOptions
{
    public const string SectionName = "Kafka";
    
    /// <summary>
    /// Kafka bootstrap servers (comma-separated list)
    /// </summary>
    public required string BootstrapServers { get; init; }
    
    /// <summary>
    /// Topic for Device Messages (Types: 2, 11, 13)
    /// </summary>
    public required string DeviceMessageTopic { get; init; }
    
    /// <summary>
    /// Topic for Device Events (Types: 1, 3, 12, 14)  
    /// </summary>
    public required string DeviceEventTopic { get; init; }
    
    /// <summary>
    /// Producer client ID for identification
    /// </summary>
    public string ClientId { get; init; } = "message-sender-service";
    
    /// <summary>
    /// Maximum time to wait for message delivery (ms)
    /// </summary>
    public int MessageTimeoutMs { get; init; } = 30000;
    
    /// <summary>
    /// Number of acknowledgments the producer requires
    /// </summary>
    public string Acks { get; init; } = "all";
    
    /// <summary>
    /// Number of retries for failed messages
    /// </summary>
    public int Retries { get; init; } = 3;
    
    /// <summary>
    /// Enable idempotence to prevent duplicate messages
    /// </summary>
    public bool EnableIdempotence { get; init; } = true;
    
    /// <summary>
    /// Compression type for messages
    /// </summary>
    public string CompressionType { get; init; } = "snappy";
}