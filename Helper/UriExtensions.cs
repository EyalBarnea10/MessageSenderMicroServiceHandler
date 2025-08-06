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
        /// <param name="deviceId">Device identifier (4 bytes)</param>
        /// <param name="messageCounter">Message counter</param>
        /// <param name="messageType">Message type</param>
        /// <param name="payload">Message payload</param>
        /// <returns>True if message was sent successfully, false if it failed</returns>
        public static bool TrySendMessage(this Uri address, byte[] deviceId, ushort messageCounter, byte messageType, byte[] payload)
        {
            try
            {
                MessageSender.Tests.MessageSender.SendMessage(address, deviceId, messageCounter, messageType, payload);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Sends test stream to the specified URI address
        /// </summary>
        /// <param name="address">The target URI address</param>
        /// <returns>True if stream was sent successfully, false if it failed</returns>
        public static bool TrySendStream(this Uri address)
        {
            try
            {
                MessageSender.Tests.MessageSender.SendStream(address);
                return true;
            }
            catch
            {
                return false;
            }
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