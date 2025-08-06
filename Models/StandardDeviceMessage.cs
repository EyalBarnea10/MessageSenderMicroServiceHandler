using System.Text.Json.Serialization;

namespace Models;

/// <summary>
/// Standardized JSON format for Device Messages (Types: 2, 11, 13)
/// Represents the business domain model for device messages
/// </summary>
public record StandardDeviceMessage
{
    [JsonPropertyName("deviceId")]
    public required string DeviceId { get; init; }
    
    [JsonPropertyName("messageCounter")]
    public required ushort MessageCounter { get; init; }
    
    [JsonPropertyName("messageType")]
    public required byte MessageType { get; init; }
    
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }
    
    [JsonPropertyName("payload")]
    public required string Payload { get; init; }
    
    [JsonPropertyName("payloadSize")]
    public required int PayloadSize { get; init; }
    
    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }
}

/// <summary>
/// Raw Device Event for direct routing (Types: 1, 3, 12, 14)
/// Minimal wrapper for event data
/// </summary>
public record DeviceEvent
{
    [JsonPropertyName("deviceId")]
    public required string DeviceId { get; init; }
    
    [JsonPropertyName("messageCounter")]
    public required ushort MessageCounter { get; init; }
    
    [JsonPropertyName("messageType")]
    public required byte MessageType { get; init; }
    
    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }
    
    [JsonPropertyName("eventData")]
    public required byte[] EventData { get; init; }
    
    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }
}