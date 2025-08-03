using System.Buffers;
using System.Net.Sockets;
using System.Buffers.Binary;
using MessageSender.Helper;

namespace MessageSender.Tests
{
    // Modern record for device message
    public readonly record struct DeviceMessageRequest(
        ReadOnlyMemory<byte> DeviceId,
        ushort MessageCounter,
        byte MessageType,
        ReadOnlyMemory<byte> Payload)
    {
        public void Validate()
        {
            if (DeviceId.Length != 4)
                throw new ArgumentException("Device ID must be exactly 4 bytes", nameof(DeviceId));
            if (Payload.Length > ushort.MaxValue)
                throw new ArgumentException($"Payload too large. Max size: {ushort.MaxValue} bytes", nameof(Payload));
        }
    }

    public class DeviceMessageException : Exception
    {
        public DeviceMessageException(string message) : base(message) { }
        public DeviceMessageException(string message, Exception innerException) : base(message, innerException) { }
    }

    public static class MessageSender
    {
        private const ushort SyncWord = 0xAA55;
        private const int HeaderSize = 11; // 2 + 4 + 2 + 1 + 2

        // Modern async API
        public static async Task SendMessageAsync(Uri address, DeviceMessageRequest message, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(address);
            message.Validate();

            using var client = new TcpClient();
            try
            {
                // Modern timeout configuration
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 5000;

                await client.ConnectAsync(address.Host, address.Port, cancellationToken).ConfigureAwait(false);
                await using var stream = client.GetStream();

                // Use ArrayPool for efficient memory management
                var messageSize = HeaderSize + message.Payload.Length;
                var buffer = ArrayPool<byte>.Shared.Rent(messageSize);
                try
                {
                    var messageBytes = buffer.AsSpan(0, messageSize);
                    BuildMessage(message, messageBytes);
                    
                    await stream.WriteAsync(buffer.AsMemory(0, messageSize), cancellationToken).ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                throw new DeviceMessageException($"Failed to send message to {address}", ex);
            }
        }

        // Legacy sync wrapper for backward compatibility
        public static void SendMessage(Uri address, byte[] deviceId, short messageCounter, byte messageType, byte[] payload)
        {
            var request = new DeviceMessageRequest(deviceId, (ushort)messageCounter, messageType, payload);
            SendMessageAsync(address, request).GetAwaiter().GetResult();
        }

        // Efficient message building using Span<T> and BinaryPrimitives
        private static void BuildMessage(DeviceMessageRequest message, Span<byte> buffer)
        {
            var writer = buffer;
            
            // Sync Word (Big Endian)
            BinaryPrimitives.WriteUInt16BigEndian(writer, SyncWord);
            writer = writer[2..];
            
            // Device ID
            message.DeviceId.Span.CopyTo(writer);
            writer = writer[4..];
            
            // Message Counter (Big Endian)
            BinaryPrimitives.WriteUInt16BigEndian(writer, message.MessageCounter);
            writer = writer[2..];
            
            // Message Type
            writer[0] = message.MessageType;
            writer = writer[1..];
            
            // Payload Length (Big Endian)
            BinaryPrimitives.WriteUInt16BigEndian(writer, (ushort)message.Payload.Length);
            writer = writer[2..];
            
            // Payload
            message.Payload.Span.CopyTo(writer);
        }

        // Modern async batch sending
        public static async Task SendStreamAsync(Uri address, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(address);

            ReadOnlyMemory<byte> firstDeviceId = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            ReadOnlyMemory<byte> secondDeviceId = new byte[] { 0x05, 0x06, 0x07, 0x08 };

            var messages = new[]
            {
                new DeviceMessageRequest(firstDeviceId, 1, 0x01, new byte[] { 0x01, 0x02, 0x03 }),
                new DeviceMessageRequest(firstDeviceId, 1, 0x01, new byte[] { 0x01, 0x02, 0x03 }), // duplicate
                new DeviceMessageRequest(firstDeviceId, 1, 0x02, new byte[] { 0x07, 0x08, 0x09 }),
                new DeviceMessageRequest(secondDeviceId, 1, 0x01, new byte[] { 0x04, 0x05, 0x06 })
            };

            // Send all messages concurrently for better performance
            var sendTasks = messages.Select(msg => SendMessageAsync(address, msg, cancellationToken));
            await Task.WhenAll(sendTasks).ConfigureAwait(false);
        }

        // Legacy sync methods for backward compatibility
        public static void SendStream(string address)
        {
            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
            {
                throw new ArgumentException("Invalid URI format. Use 'tcp://hostname:port' format.", nameof(address));
            }

            SendStreamAsync(uri).GetAwaiter().GetResult();
        }

        public static void SendStream(Uri address)
            => SendStreamAsync(address).GetAwaiter().GetResult();
    }

}