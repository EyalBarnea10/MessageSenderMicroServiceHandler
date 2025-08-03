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

var builder = WebApplication.CreateBuilder(args);

// Add configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: false)
                    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
                    .AddEnvironmentVariables();

// Add services
builder.Services.Configure<TcpListenerOptions>(
    builder.Configuration.GetSection("TcpListener"));

builder.Services.AddSingleton<IMessageHandler, KafkaMessageHandler>();
builder.Services.AddSingleton<IBinaryListener>(sp =>
{
    var options = sp.GetRequiredService<IOptions<TcpListenerOptions>>().Value;
    var handler = sp.GetRequiredService<IMessageHandler>();
    var logger = sp.GetRequiredService<ILogger<TcpBinaryListener>>();
    return new TcpBinaryListener(options.Port, handler, logger);
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


builder.Services.AddOpenTelemetry()
        .WithMetrics(metrics =>
    {
        metrics.AddMeter("TcpListener.Metrics")
               .AddPrometheusExporter();
    });

builder.Services.AddOpenTelemetry();

var app = builder.Build();

// Configure endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthChecks.UI.Client.UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapMetrics();

await app.RunAsync();

// Build and run locally
// dotnet run

// Build and run with Docker
// docker build -t tcp-listener-service .
// docker run -p 80:80 -p 5000:5000 tcp-listener-service
