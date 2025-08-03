public class TcpListenerOptions
{
    public int Port { get; set; } = 5000;
    public int MaxConcurrentClients { get; set; } = 100;
    public int BufferSize { get; set; } = 4096;
    public int ClientTimeoutMs { get; set; } = 30000;  // 30 seconds
    public int MaxBufferSizePerClient { get; set; } = 1024 * 1024; // 1MB
    public int DeduplicationCacheMaxSizePerDevice { get; set; } = 1000;

    public override string ToString()
    {
        return $"Port: {Port}, MaxConcurrentClients: {MaxConcurrentClients}, BufferSize: {BufferSize}, " +
               $"ClientTimeout: {ClientTimeoutMs}ms, MaxBufferPerClient: {MaxBufferSizePerClient} bytes";
    }
}   