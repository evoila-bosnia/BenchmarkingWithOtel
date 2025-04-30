using BenchmarkingWithOtel.Client;
using BenchmarkingWithOtel.Client.Services;
using System.Diagnostics;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Register OpenTelemetry activity source
var activitySource = new ActivitySource("BenchmarkingWithOtel.Client");
builder.Services.AddSingleton(activitySource);

// Add OpenTelemetry sources
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder.AddSource("BenchmarkingWithOtel.Client");
        tracerProviderBuilder.AddSource("BenchmarkingWithOtel.Client.Benchmark");
        tracerProviderBuilder.AddSource("BenchmarkingWithOtel.Client.Runner");
        tracerProviderBuilder.AddSource("BenchmarkingWithOtel.Client.Worker");
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
    builder.Configuration["Benchmark:OperationCount"] = "1000";
    builder.Configuration["Benchmark:Iterations"] = "3";
    builder.Configuration["Benchmark:DelayBetweenRuns"] = "5000";
}

// Register worker
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
