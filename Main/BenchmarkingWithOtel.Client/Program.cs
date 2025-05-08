using BenchmarkingWithOtel.Client;
using BenchmarkingWithOtel.Client.Services;
using BenchmarkingWithOtel.Client.Telemetry;
using System.Diagnostics;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Context.Propagation;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Enable W3C propagation for distributed tracing
Activity.DefaultIdFormat = ActivityIdFormat.W3C;
Activity.ForceDefaultIdFormat = true;

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

// Register HTTP client for the server with propagation headers
builder.Services.AddHttpClient<BenchmarkService>(client =>
{
    // The URI will be provided by Aspire service discovery
    client.BaseAddress = new Uri(builder.Configuration.GetValue<string>("ServerUrl", "http://benchmarkingwithotel-server"));
    client.Timeout = TimeSpan.FromSeconds(30);
    
    // Add default headers to identify client
    client.DefaultRequestHeaders.Add("X-Benchmark-Client", "BenchmarkingWithOtel.Client");
});

// Register benchmark services
builder.Services.AddSingleton<BenchmarkRunner>();

// Configure default benchmark settings if not provided
if (!builder.Configuration.GetSection("Benchmark").GetChildren().Any())
{
    builder.Configuration["Benchmark:OperationCount"] = "1000";
    builder.Configuration["Benchmark:Iterations"] = "3";
    builder.Configuration["Benchmark:DelayBetweenRuns"] = "5000";
}

// Register worker
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
