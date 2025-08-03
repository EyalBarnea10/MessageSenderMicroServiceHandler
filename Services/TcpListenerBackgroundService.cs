using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

public class TcpListenerBackgroundService : BackgroundService
{
    private readonly IBinaryListener _listener;
    private readonly ILogger<TcpListenerBackgroundService> _logger;

    public TcpListenerBackgroundService(
        IBinaryListener listener,
        ILogger<TcpListenerBackgroundService> logger)
    {
        _listener = listener;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting TCP Listener Service");
            await _listener.StartAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in TCP Listener Service");
            throw;
        }
    }
}