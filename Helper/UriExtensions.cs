using MessageSender.Tests;

namespace MessageSender.Helper
{
    /// <summary>
    /// Extension methods for Uri to provide convenient device message sending functionality
    /// </summary>
    public static class UriExtensions
    {
        /// <summary>
        /// Attempts to send a device message to the specified URI address
        /// </summary>
        /// <param name="address">The target URI address</param>
        /// <param name="message">The device message to send</param>
        /// <param name="cancellationToken">Cancellation token for async operation</param>
        /// <returns>True if message was sent successfully, false if it failed</returns>
        public static async Task<bool> TrySendMessageAsync(this Uri address, DeviceMessageRequest message, CancellationToken cancellationToken = default)
        {
            try
            {
                await MessageSender.Tests.MessageSender.SendMessageAsync(address, message, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (DeviceMessageException)
            {
                return false;
            }
        }

        /// <summary>
        /// Sends a device message to the specified URI address (sync version)
        /// </summary>
        /// <param name="address">The target URI address</param>
        /// <param name="message">The device message to send</param>
        /// <returns>True if message was sent successfully, false if it failed</returns>
        public static bool TrySendMessage(this Uri address, DeviceMessageRequest message)
        {
            try
            {
                // Use modern async API synchronously
                MessageSender.Tests.MessageSender.SendMessageAsync(address, message).GetAwaiter().GetResult();
                return true;
            }
            catch (DeviceMessageException)
            {
                return false;
            }
        }

        /// <summary>
        /// Creates a device message request with fluent API
        /// </summary>
        /// <param name="address">The target URI address</param>
        /// <param name="deviceId">4-byte device identifier</param>
        /// <param name="messageCounter">Message counter</param>
        /// <param name="messageType">Message type</param>
        /// <param name="payload">Message payload</param>
        /// <returns>DeviceMessageRequest for further operations</returns>
        public static DeviceMessageRequest CreateMessage(this Uri address, ReadOnlyMemory<byte> deviceId, ushort messageCounter, byte messageType, ReadOnlyMemory<byte> payload)
        {
            return new DeviceMessageRequest(deviceId, messageCounter, messageType, payload);
        }

        /// <summary>
        /// Validates if the URI is suitable for TCP message sending
        /// </summary>
        /// <param name="address">The URI to validate</param>
        /// <returns>True if valid for TCP communication</returns>
        public static bool IsValidTcpAddress(this Uri address)
        {
            return address != null && 
                   !string.IsNullOrEmpty(address.Host) && 
                   address.Port > 0 && 
                   address.Port <= 65535;
        }
    }
}