using Microsoft.Extensions.Logging;
using System.Text.Json;

public class KafkaMessageHandler : IMessageHandler
{
    private readonly ILogger<KafkaMessageHandler> _logger;

    public KafkaMessageHandler(ILogger<KafkaMessageHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(DeviceMessage message, CancellationToken cancellationToken)
    {
        // For now, just log the message. In a real implementation, this would
        // publish to Kafka using a Kafka producer client.
        try
        {
            var messageJson = JsonSerializer.Serialize(new
            {
                DeviceId = BitConverter.ToString(message.DeviceId),
                MessageCounter = message.MessageCounter,
                MessageType = message.MessageType,
                PayloadLength = message.Payload?.Length ?? 0,
                Payload = message.Payload != null ? Convert.ToBase64String(message.Payload) : null,
                Timestamp = DateTimeOffset.UtcNow
            });

            _logger.LogInformation("Processing device message: {Message}", messageJson);
            
            // TODO: Implement actual Kafka publishing here
            // Example:
            // await _kafkaProducer.ProduceAsync(_topicName, new Message<string, string>
            // {
            //     Key = BitConverter.ToString(message.DeviceId),
            //     Value = messageJson
            // }, cancellationToken);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle device message for DeviceId {DeviceId}", 
                BitConverter.ToString(message.DeviceId));
            throw;
        }
    }
}