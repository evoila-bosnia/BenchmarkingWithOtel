var builder = DistributedApplication.CreateBuilder(args);

var database = builder.AddOracle("oracledb").WithImageTag("latest-lite");

var server = builder.AddProject<Projects.BenchmarkingWithOtel_Server>("benchmarkingwithotel-server")
    .WithReference(database)
    .WaitFor(database);

builder.AddProject<Projects.BenchmarkingWithOtel_Client>("benchmarkingwithotel-client")
    .WithReference(server)
    .WaitFor(server);

builder.Build().Run();
