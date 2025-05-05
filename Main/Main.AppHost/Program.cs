var builder = DistributedApplication.CreateBuilder(args);

var database = builder.AddOracle("oracledb").WithImageTag("latest-lite");

var proxy = builder.AddProject<Projects.BenchmarkingWithOtel_ReverseProxy>("benchmarkingwithotel-reverseproxy");

var server = builder.AddProject<Projects.BenchmarkingWithOtel_Server>("benchmarkingwithotel-server")
    .WithReference(database)
    .WaitFor(database);

builder.AddProject<Projects.BenchmarkingWithOtel_Client>("benchmarkingwithotel-client")
    .WithReference(server)
    .WithReference(proxy)
    .WaitFor(server)
    .WaitFor(proxy);



builder.Build().Run();
