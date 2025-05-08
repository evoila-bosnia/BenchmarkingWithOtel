using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var database = builder.AddOracle("oracledb").WithImageTag("latest-lite");

var proxy = builder.AddProject<Projects.BenchmarkingWithOtel_ReverseProxy>("benchmarkingwithotel-reverseproxy");

var server = builder.AddProject<Projects.BenchmarkingWithOtel_Server>("benchmarkingwithotel-server")
    .WithEnvironment("OTEL_SERVICE_NAME", "benchmarkingwithotel-server")
    //.WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf")
    //.WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "https://api.honeycomb.io")
    //.WithEnvironment("OTEL_EXPORTER_OTLP_HEADERS", "x-honeycomb-team=FqTl7veAc5wvA5mRUJQYVD")
    //.WithEnvironment("OTEL_LOG_LEVEL", "debug")
    //.WithEnvironment("OTEL_EXPORTER_OTLP_TRACES_TEMPORALITY_PREFERENCE", "delta")
    .WithEnvironment("OTEL_TRACES_EXPORTER", "otlp")
    .WithEnvironment("OTEL_METRICS_EXPORTER", "otlp")
    .WithEnvironment("OTEL_LOGS_EXPORTER", "otlp,console")
    .WithReference(database)
    .WaitFor(database);

builder.AddProject<Projects.BenchmarkingWithOtel_Client>("benchmarkingwithotel-client")
    .WithEnvironment("OTEL_SERVICE_NAME", "benchmarkingwithotel-client")
    //.WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf")
    //.WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", "https://api.honeycomb.io")
    //.WithEnvironment("OTEL_EXPORTER_OTLP_HEADERS", "x-honeycomb-team=FqTl7veAc5wvA5mRUJQYVD")
    //.WithEnvironment("OTEL_LOG_LEVEL", "debug")
    //.WithEnvironment("OTEL_EXPORTER_OTLP_TRACES_TEMPORALITY_PREFERENCE", "delta")
    .WithEnvironment("OTEL_TRACES_EXPORTER", "otlp")
    .WithEnvironment("OTEL_METRICS_EXPORTER", "otlp")
    .WithEnvironment("OTEL_LOGS_EXPORTER", "otlp,console")
    .WithReference(server)
    .WithReference(proxy)
    .WaitFor(server)
    .WaitFor(proxy);

builder.Build().Run();
