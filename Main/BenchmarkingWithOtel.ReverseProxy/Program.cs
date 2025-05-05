var builder = WebApplication.CreateBuilder(args);

builder.Services
       .AddReverseProxy()
       .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.AddServiceDefaults();

var app = builder.Build();

app.MapGet("/test", () => Results.Ok("YARP Proxy Running"));

app.MapReverseProxy();


app.Run();

