using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

public class DeviceMessageParseException : Exception
{
    public DeviceMessageParseException(string message) : base(message) { }
}

public class TcpBinaryListener : IBinaryListener
{
    private readonly int _port;
    private readonly IMessageHandler _handler;
    private readonly ILogger<TcpBinaryListener> _logger;
    private readonly ConcurrentDictionary<string, HashSet<ushort>> _deduplicationCache = new();

    public TcpBinaryListener(int port, IMessageHandler handler, ILogger<TcpBinaryListener> logger)
    {
        _port = port;
        _handler = handler;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Any, _port);
        listener.Start();
        var clients = new ConcurrentBag<Task>();
        _logger.LogInformation("TCP Listener started on port {Port}", _port);

        while (!cancellationToken.IsCancellationRequested)
        {
            var client = await listener.AcceptTcpClientAsync(cancellationToken);
            _logger.LogInformation("Accepted new client from {Endpoint}", client.Client.RemoteEndPoint);
            clients.Add(HandleClientAsync(client, cancellationToken));
        }
        await Task.WhenAll(clients);
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var stream = client.GetStream();
        var buffer = new byte[4096];
        var messageBuffer = new List<byte>();
        _logger.LogInformation("Handling client {Endpoint}", client.Client.RemoteEndPoint);
        try
        {
            while (!cancellationToken.IsCancellationRequested && client.Connected)
            {
                int bytesRead = await stream.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0) break;
                messageBuffer.AddRange(buffer.Take(bytesRead));
                while (TryExtractMessage(ref messageBuffer, out var messageData))
                {
                    DeviceMessage deviceMessage = null;
                    try
                    {
                        deviceMessage = ParseDeviceMessage(messageData);
                    }
                    catch (DeviceMessageParseException ex)
                    {
                        _logger.LogError(ex, "Business error: Failed to parse DeviceMessage from client {Endpoint}", client.Client.RemoteEndPoint);
                        continue;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unexpected error during DeviceMessage parsing from client {Endpoint}", client.Client.RemoteEndPoint);
                        continue;
                    }
                    try
                    {
                        if (IsDuplicate(deviceMessage))
                        {
                            _logger.LogInformation("Duplicate message detected for DeviceId {DeviceId} Counter {Counter}", BitConverter.ToString(deviceMessage.DeviceId), deviceMessage.MessageCounter);
                            continue;
                        }
                        _logger.LogInformation("Received DeviceMessage from {Endpoint}: DeviceId={DeviceId}, Counter={Counter}, Type={Type}, PayloadLength={PayloadLength}", client.Client.RemoteEndPoint, BitConverter.ToString(deviceMessage.DeviceId), deviceMessage.MessageCounter, deviceMessage.MessageType, deviceMessage.Payload?.Length ?? 0);
                        await _handler.HandleAsync(deviceMessage, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Business error: Failed to handle DeviceMessage for DeviceId {DeviceId} Counter {Counter}", BitConverter.ToString(deviceMessage.DeviceId), deviceMessage.MessageCounter);
                        continue;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client {Endpoint}", client.Client.RemoteEndPoint);
        }
        finally
        {
            _logger.LogInformation("Client {Endpoint} disconnected", client.Client.RemoteEndPoint);
            client.Dispose();
        }
    }

    private DeviceMessage ParseDeviceMessage(byte[] data)
    {
        // [SyncWord(2)][DeviceId(4)][Counter(2)][Type(1)][Length(2)][Payload]
        if (data.Length < 11) throw new DeviceMessageParseException("Message too short");
        ushort syncWord = BitConverter.ToUInt16(data, 0);
        if (syncWord != DeviceMessage.SyncWord) throw new DeviceMessageParseException("Invalid sync word");
        var deviceId = data.Skip(2).Take(4).ToArray();
        var messageCounter = BitConverter.ToUInt16(data, 6);
        var messageType = data[8];
        var payloadLength = BitConverter.ToUInt16(data, 9);
        if (data.Length < 11 + payloadLength) throw new DeviceMessageParseException("Payload length mismatch");
        var payload = data.Skip(11).Take(payloadLength).ToArray();
        return new DeviceMessage
        {
            DeviceId = deviceId,
            MessageCounter = messageCounter,
            MessageType = messageType,
            Payload = payload
        };
    }

    private bool IsDuplicate(DeviceMessage message)
    {
        var deviceKey = BitConverter.ToString(message.DeviceId);
        var counters = _deduplicationCache.GetOrAdd(deviceKey, _ => new HashSet<ushort>());
        lock (counters)
        {
            if (counters.Contains(message.MessageCounter))
            {
                return true;
            }
            counters.Add(message.MessageCounter);
            // Optionally, limit cache size for each device
            if (counters.Count > 1000)
            {
                var oldest = counters.OrderBy(x => x).Take(counters.Count - 1000).ToList();
                foreach (var old in oldest) counters.Remove(old);
            }
            return false;
        }
    }

    private bool TryExtractMessage(ref List<byte> buffer, out byte[] messageData)
    {
        messageData = null;
        if (buffer.Count < 11) return false; // Minimum size
        for (int i = 0; i < buffer.Count - 1; i++)
        {
            if (buffer[i] == 0xAA && buffer[i + 1] == 0x55)
            {
                if (buffer.Count < i + 11) continue;
                int payloadLength = BitConverter.ToUInt16(buffer.Skip(i + 9).Take(2).ToArray(), 0);
                int totalLength = 11 + payloadLength;
                if (buffer.Count < i + totalLength) continue;
                messageData = buffer.Skip(i).Take(totalLength).ToArray();
                buffer.RemoveRange(0, i + totalLength);
                return true;
            }
        }
        return false;
    }
}