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
    private readonly TcpListenerOptions _options;
    private readonly ConcurrentDictionary<string, HashSet<ushort>> _deduplicationCache = new();
    private readonly SemaphoreSlim _connectionSemaphore;

    public TcpBinaryListener(int port, IMessageHandler handler, ILogger<TcpBinaryListener> logger, TcpListenerOptions? options = null)
    {
        _port = port;
        _handler = handler;
        _logger = logger;
        _options = options ?? new TcpListenerOptions();
        _connectionSemaphore = new SemaphoreSlim(_options.MaxConcurrentClients, _options.MaxConcurrentClients);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var listener = new TcpListener(IPAddress.Any, _port);
        listener.Start();
        var clientTasks = new List<Task>();
        _logger.LogInformation("TCP Listener started on port {Port} with max {MaxClients} concurrent connections", 
            _port, _options.MaxConcurrentClients);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken);
                
                // Clean up completed tasks to prevent memory leak
                clientTasks.RemoveAll(t => t.IsCompleted);
                
                // Check connection limit
                if (!await _connectionSemaphore.WaitAsync(0, cancellationToken))
                {
                    _logger.LogWarning("Connection limit reached ({MaxClients}), rejecting client {Endpoint}", 
                        _options.MaxConcurrentClients, client.Client.RemoteEndPoint);
                    client.Close();
                    continue;
                }

                _logger.LogInformation("Accepted new client from {Endpoint} ({ActiveConnections}/{MaxConnections})", 
                    client.Client.RemoteEndPoint, 
                    _options.MaxConcurrentClients - _connectionSemaphore.CurrentCount,
                    _options.MaxConcurrentClients);
                
                var clientTask = HandleClientAsync(client, cancellationToken);
                clientTasks.Add(clientTask);
            }
        }
        finally
        {
            listener.Stop();
            // Wait for all client tasks to complete
            if (clientTasks.Count > 0)
            {
                await Task.WhenAll(clientTasks.Where(t => !t.IsCompleted));
            }
            _connectionSemaphore?.Dispose();
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = client.GetStream();
            var buffer = new byte[_options.BufferSize];
            var messageBuffer = new List<byte>();
            
            // Set timeouts from configuration
            client.ReceiveTimeout = _options.ClientTimeoutMs;
            client.SendTimeout = _options.ClientTimeoutMs;
            
            _logger.LogInformation("Handling client {Endpoint}", client.Client.RemoteEndPoint);

            while (!cancellationToken.IsCancellationRequested && client.Connected)
            {
                int bytesRead = await stream.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0) break;

                // Prevent buffer overflow attacks
                if (messageBuffer.Count + bytesRead > _options.MaxBufferSizePerClient)
                {
                    _logger.LogWarning("Client {Endpoint} exceeded buffer limit ({MaxSize} bytes), disconnecting", 
                        client.Client.RemoteEndPoint, _options.MaxBufferSizePerClient);
                    break;
                }

                messageBuffer.AddRange(buffer.Take(bytesRead));
                
                while (TryExtractMessage(ref messageBuffer, out var messageData) && messageData != null)
                {
                    DeviceMessage? deviceMessage = null;
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
                            _logger.LogInformation("Duplicate message detected for DeviceId {DeviceId} Counter {Counter}", 
                                BitConverter.ToString(deviceMessage.DeviceId), deviceMessage.MessageCounter);
                            continue;
                        }
                        
                        _logger.LogInformation("Received DeviceMessage from {Endpoint}: DeviceId={DeviceId}, Counter={Counter}, Type={Type}, PayloadLength={PayloadLength}", 
                            client.Client.RemoteEndPoint, BitConverter.ToString(deviceMessage.DeviceId), 
                            deviceMessage.MessageCounter, deviceMessage.MessageType, deviceMessage.Payload?.Length ?? 0);
                        
                        await _handler.HandleAsync(deviceMessage, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Business error: Failed to handle DeviceMessage for DeviceId {DeviceId} Counter {Counter}", 
                            BitConverter.ToString(deviceMessage.DeviceId), deviceMessage.MessageCounter);
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
            _connectionSemaphore.Release(); // Critical: Release the semaphore
        }
    }

    private DeviceMessage ParseDeviceMessage(byte[] data)
    {
        // [SyncWord(2)][DeviceId(4)][Counter(2)][Type(1)][Length(2)][Payload]
        if (data.Length < 11) throw new DeviceMessageParseException("Message too short");
        
        // Convert from Big Endian to host endianness
        ushort syncWord = (ushort)((data[0] << 8) | data[1]);
        if (syncWord != DeviceMessage.SyncWord) throw new DeviceMessageParseException("Invalid sync word");
        
        var deviceId = data.Skip(2).Take(4).ToArray();
        
        // Convert from Big Endian for multi-byte fields
        var messageCounter = (ushort)((data[6] << 8) | data[7]);
        var messageType = data[8];
        var payloadLength = (ushort)((data[9] << 8) | data[10]);
        
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
            // Limit cache size for each device to prevent memory growth
            if (counters.Count > _options.DeduplicationCacheMaxSizePerDevice)
            {
                var excessCount = counters.Count - _options.DeduplicationCacheMaxSizePerDevice;
                var oldest = counters.OrderBy(x => x).Take(excessCount).ToList();
                foreach (var old in oldest) counters.Remove(old);
                _logger.LogDebug("Cleaned up {Count} old message counters for device {DeviceId}", 
                    oldest.Count, BitConverter.ToString(message.DeviceId));
            }
            return false;
        }
    }

    private bool TryExtractMessage(ref List<byte> buffer, out byte[]? messageData)
    {
        messageData = null;
        if (buffer.Count < 11) return false; // Minimum size
        for (int i = 0; i < buffer.Count - 1; i++)
        {
            if (buffer[i] == 0xAA && buffer[i + 1] == 0x55)
            {
                if (buffer.Count < i + 11) continue;
                // Extract payload length in Big Endian format
                int payloadLength = (buffer[i + 9] << 8) | buffer[i + 10];
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