public interface IMessageHandler
{
    Task HandleAsync(DeviceMessage message, CancellationToken cancellationToken);
}