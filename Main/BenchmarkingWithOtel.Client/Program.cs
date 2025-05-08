using BenchmarkingWithOtel.Client;
using BenchmarkingWithOtel.Client.Services;
using BenchmarkingWithOtel.Client.Telemetry;
using System.Diagnostics;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Create a shared resource for OpenTelemetry
var otelResource = ResourceBuilder.CreateDefault()
    .AddService("BenchmarkingWithOtel.Client")
    .AddAttributes(new Dictionary<string, object>
    {
        ["service.instance.id"] = Environment.MachineName,
        ["deployment.environment"] = builder.Environment.EnvironmentName
    })
    .Build();

// Register OpenTelemetry activity source
var activitySource = new ActivitySource("BenchmarkingWithOtel.Client");
builder.Services.AddSingleton(activitySource);

// Register metrics service
builder.Services.AddSingleton<MetricsService>();

// Configure OpenTelemetry with resource sharing
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.Clear().AddAttributes(otelResource.Attributes))
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .AddSource("BenchmarkingWithOtel.Client")
            .AddSource("BenchmarkingWithOtel.Client.Benchmark")
            .AddSource("BenchmarkingWithOtel.Client.Runner")
            .AddSource("BenchmarkingWithOtel.Client.Worker");
    })
    .WithMetrics(metricsProviderBuilder =>
    {
        metricsProviderBuilder
            .AddMeter("BenchmarkingWithOtel.Client.Metrics");
    });

// Configure logging to include trace context
builder.Logging.AddOpenTelemetry(options =>
{
    options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddAttributes(otelResource.Attributes));
    options.IncludeScopes = true;
    options.IncludeFormattedMessage = true;
    options.ParseStateValues = true;
});

// Register HTTP client for the server
builder.Services.AddHttpClient<BenchmarkService>(client =>
{
    // The URI will be provided by Aspire service discovery
    client.BaseAddress = new Uri(builder.Configuration.GetValue<string>("ServerUrl", "http://benchmarkingwithotel-server"));
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Register benchmark services
builder.Services.AddSingleton<BenchmarkRunner>();

// Configure default benchmark settings if not provided
if (!builder.Configuration.GetSection("Benchmark").GetChildren().Any())
{
    builder.Configuration["Benchmark:OperationCount"] = "10";
    builder.Configuration["Benchmark:Iterations"] = "1";
    builder.Configuration["Benchmark:DelayBetweenRuns"] = "50";
}

// Register worker
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
