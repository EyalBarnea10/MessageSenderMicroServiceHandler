using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Prometheus;
using Microsoft.Extensions.Options;
using Configuration;
using Services;
using Microsoft.AspNetCore.Http;
using MessageSender.Tests;


var builder = WebApplication.CreateBuilder(args);

// Add configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: false)
                    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
                    .AddEnvironmentVariables();

// Add services
builder.Services.Configure<TcpListenerOptions>(
    builder.Configuration.GetSection("TcpListener"));

// Configure Kafka options with validation
builder.Services.Configure<KafkaOptions>(
    builder.Configuration.GetSection(KafkaOptions.SectionName));

// Add services with proper DI registration
builder.Services.AddSingleton<MessageProcessingMetrics>();
        // Register real Kafka message handler
        builder.Services.AddSingleton<IMessageHandler, KafkaMessageHandler>();
builder.Services.AddSingleton<IBinaryListener>(sp =>
{
    var options = sp.GetRequiredService<IOptions<TcpListenerOptions>>().Value; //to get data from config
    var handler = sp.GetRequiredService<IMessageHandler>();
    var logger = sp.GetRequiredService<ILogger<TcpBinaryListener>>();
    var metrics = sp.GetRequiredService<MessageProcessingMetrics>();
    return new TcpBinaryListener(options.Port, handler, logger, metrics, options);
});
builder.Services.AddHostedService<TcpListenerBackgroundService>();

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck("tcp_listener", () => 
        HealthCheckResult.Healthy("TCP Listener is running"));

// Add OpenTelemetry Metrics and Tracing
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("TcpListener.Metrics")
               .AddPrometheusExporter(); // For Prometheus metrics
    })
    .WithTracing(tracing =>
    {
        tracing.AddSource("TcpListener.Traces")
               .AddJaegerExporter(); // For Jaeger tracing
    });

var app = builder.Build();

// Configure endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthChecks.UI.Client.UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapMetrics();

// Add test endpoint for MessageSender testing
app.MapPost("/test-messagesender", async (HttpContext context) =>
{
    try
    {
        var address = "tcp://localhost:5000";
        
        
        // Test individual message sending
        byte[] deviceId = { 0x01, 0x02, 0x03, 0x04 };
        MessageSender.Tests.MessageSender.SendMessage(new Uri(address), deviceId, 1, 2, new byte[] { 0x01, 0x02, 0x03 });
        
        // Test stream sending
        MessageSender.Tests.MessageSender.SendStream(address);
        
        return Results.Ok(new { message = "MessageSender test completed successfully", address });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

await app.RunAsync();

// Build and run locally
// dotnet run

// Build and run with Docker
// docker build -t tcp-listener-service .
// docker run -p 80:80 -p 5000:5000 tcp-listener-service
