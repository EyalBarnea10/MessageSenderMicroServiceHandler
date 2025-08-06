using System.Diagnostics.Metrics;

namespace Services;

/// <summary>
/// Centralized metrics for message processing operations
/// Follows SRP by handling only metric collection concerns
/// </summary>
public class MessageProcessingMetrics : IDisposable
{
    private readonly Meter _meter;
    
    // Counters for different message types
    private readonly Counter<long> _deviceMessagesProcessed;
    private readonly Counter<long> _deviceEventsProcessed;
    private readonly Counter<long> _duplicateMessagesRejected;
    private readonly Counter<long> _invalidMessagesRejected;
    private readonly Counter<long> _kafkaPublishErrors;
    
    // Histograms for performance tracking
    private readonly Histogram<double> _messageProcessingDuration;
    private readonly Histogram<double> _kafkaPublishDuration;
    
    // Gauges for current state
    private readonly UpDownCounter<int> _activeConnections;

    public MessageProcessingMetrics()
    {
        _meter = new Meter("TcpListener.Metrics", "1.0.0");
        
        // Initialize counters
        _deviceMessagesProcessed = _meter.CreateCounter<long>(
            "device_messages_processed_total",
            "messages",
            "Total number of device messages processed successfully");
            
        _deviceEventsProcessed = _meter.CreateCounter<long>(
            "device_events_processed_total", 
            "events",
            "Total number of device events processed successfully");
            
        _duplicateMessagesRejected = _meter.CreateCounter<long>(
            "duplicate_messages_rejected_total",
            "messages", 
            "Total number of duplicate messages rejected");
            
        _invalidMessagesRejected = _meter.CreateCounter<long>(
            "invalid_messages_rejected_total",
            "messages",
            "Total number of invalid messages rejected");
            
        _kafkaPublishErrors = _meter.CreateCounter<long>(
            "kafka_publish_errors_total",
            "errors",
            "Total number of Kafka publish errors");
        
        // Initialize histograms
        _messageProcessingDuration = _meter.CreateHistogram<double>(
            "message_processing_duration_seconds",
            "seconds",
            "Duration of message processing operations");
            
        _kafkaPublishDuration = _meter.CreateHistogram<double>(
            "kafka_publish_duration_seconds", 
            "seconds",
            "Duration of Kafka publish operations");
        
        // Initialize gauges
        _activeConnections = _meter.CreateUpDownCounter<int>(
            "active_tcp_connections",
            "connections",
            "Number of active TCP connections");
    }

    public void IncrementDeviceMessagesProcessed(string deviceId, byte messageType) =>
        _deviceMessagesProcessed.Add(1, new KeyValuePair<string, object?>("device_id", deviceId),
                                          new KeyValuePair<string, object?>("message_type", messageType));

    public void IncrementDeviceEventsProcessed(string deviceId, byte messageType) =>
        _deviceEventsProcessed.Add(1, new KeyValuePair<string, object?>("device_id", deviceId),
                                        new KeyValuePair<string, object?>("message_type", messageType));

    public void IncrementDuplicateMessagesRejected(string deviceId) =>
        _duplicateMessagesRejected.Add(1, new KeyValuePair<string, object?>("device_id", deviceId));

    public void IncrementInvalidMessagesRejected(string reason) =>
        _invalidMessagesRejected.Add(1, new KeyValuePair<string, object?>("reason", reason));

    public void IncrementKafkaPublishErrors(string topic, string error) =>
        _kafkaPublishErrors.Add(1, new KeyValuePair<string, object?>("topic", topic),
                                     new KeyValuePair<string, object?>("error", error));

    public void RecordMessageProcessingDuration(double durationSeconds, string messageType) =>
        _messageProcessingDuration.Record(durationSeconds, new KeyValuePair<string, object?>("message_type", messageType));

    public void RecordKafkaPublishDuration(double durationSeconds, string topic) =>
        _kafkaPublishDuration.Record(durationSeconds, new KeyValuePair<string, object?>("topic", topic));

    public void IncrementActiveConnections() => _activeConnections.Add(1);
    public void DecrementActiveConnections() => _activeConnections.Add(-1);

    public void Dispose()
    {
        _meter?.Dispose();
    }
}