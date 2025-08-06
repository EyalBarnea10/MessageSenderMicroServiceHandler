using System.Net.Sockets;

namespace MessageSender.Tests
{
    public class MessageSender
    {

        // change this to be using uri instead of ip address and port
        public static void SendMessage(Uri address, byte[] deviceId, ushort messageCounter, byte messageType, byte[] payload)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (deviceId == null || deviceId.Length != 4) 
                throw new ArgumentException("Device ID must be exactly 4 bytes", nameof(deviceId));
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            ushort payloadLength = (ushort)payload.Length;

            using var client = new TcpClient(address.Host, address.Port);
            using var stream = client.GetStream();

            // Write protocol data in Big Endian format as required
            var buffer = new byte[11 + payloadLength]; // Header (11 bytes) + payload
            int offset = 0;

            // Sync Word (0xAA55) - Big Endian
            buffer[offset++] = 0xAA;
            buffer[offset++] = 0x55;

            // Device ID (4 bytes) - already in correct format
            Array.Copy(deviceId, 0, buffer, offset, 4);
            offset += 4;

            // Message Counter (2 bytes) - Big Endian
            buffer[offset++] = (byte)(messageCounter >> 8);
            buffer[offset++] = (byte)(messageCounter & 0xFF);

            // Message Type (1 byte)
            buffer[offset++] = messageType;

            // Payload Length (2 bytes) - Big Endian
            buffer[offset++] = (byte)(payloadLength >> 8);
            buffer[offset++] = (byte)(payloadLength & 0xFF);

            // Payload
            Array.Copy(payload, 0, buffer, offset, payloadLength);

            stream.Write(buffer, 0, buffer.Length);
        }

        public static void SendStream(string address)
        {
            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
            {
                throw new ArgumentException("Invalid address format. Use 'hostname:port'.", nameof(address));
            }

            SendStream(uri);
        }
        public static void SendStream(Uri address)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));

            byte[] firstDeviceId = [0x01, 0x02, 0x03, 0x04];
            byte[] secondDeviceId = [0x05, 0x06, 0x07, 0x08];

            // Test different message types for validation
            // Device Messages (Types: 2, 11, 13) - should be routed to device-messages topic
            SendMessage(address, firstDeviceId, 1, 2, [0x01, 0x02, 0x03]);    // Device Message
            SendMessage(address, firstDeviceId, 2, 11, [0x04, 0x05, 0x06]);   // Device Message  
            SendMessage(address, secondDeviceId, 1, 13, [0x07, 0x08, 0x09]);  // Device Message

            // Device Events (Types: 1, 3, 12, 14) - should be routed to device-events topic  
            SendMessage(address, firstDeviceId, 3, 1, [0x0A, 0x0B]);          // Device Event
            SendMessage(address, secondDeviceId, 2, 3, [0x0C, 0x0D]);         // Device Event
            SendMessage(address, firstDeviceId, 4, 12, [0x0E, 0x0F]);         // Device Event
            SendMessage(address, secondDeviceId, 3, 14, [0x10, 0x11]);        // Device Event

            // Test duplicate message (same device + counter) - should be rejected
            SendMessage(address, firstDeviceId, 1, 2, [0x99, 0x88]);          // Duplicate

            // Test unknown message type - should be ignored
            SendMessage(address, firstDeviceId, 5, 99, [0x77]);               // Unknown type
        }

    }

}