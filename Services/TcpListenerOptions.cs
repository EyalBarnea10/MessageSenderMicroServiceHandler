public class TcpListenerOptions
{
    public int Port { get; set; } = 5000;
    public int MaxConcurrentClients { get; set; } = 100;
    public int BufferSize { get; set; } = 4096;

    public override string ToString()
    {
        return $"Port: {Port}, MaxConcurrentClients: {MaxConcurrentClients}, BufferSize: {BufferSize}";
    }
}   