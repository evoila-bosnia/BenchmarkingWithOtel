using BenchmarkingWithOtel.Server.Data;
using BenchmarkingWithOtel.Server.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Register Oracle DbContext
// Aspire will automatically inject the connection string as an environment variable
// in the format: ConnectionStrings__{sourceResourceName}={connectionString}
builder.Services.AddDbContext<BenchmarkDbContext>(options =>
    options.UseOracle(builder.Configuration.GetConnectionString("oracledb")));

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Register ActivitySource for OpenTelemetry
var benchmarkActivitySource = new ActivitySource("BenchmarkingWithOtel.Server.Api");
builder.Services.AddSingleton(benchmarkActivitySource);

// Add custom OpenTelemetry configuration
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder.AddSource(benchmarkActivitySource.Name);
        tracerProviderBuilder.AddEntityFrameworkCoreInstrumentation(options =>
        {
            options.SetDbStatementForText = true;
        });
    });

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<BenchmarkDbContext>();
    dbContext.Database.EnsureCreated();
}

// Benchmark API Endpoints
var benchmarkItems = app.MapGroup("/api/benchmark-items");

// GET all items
benchmarkItems.MapGet("/", async (BenchmarkDbContext db, ActivitySource activitySource) =>
{
    using var activity = activitySource.StartActivity("GetAllBenchmarkItems", ActivityKind.Server);
    
    try
    {
        var items = await db.BenchmarkItems.ToListAsync();
        activity?.SetTag("benchmark.items.count", items.Count);
        return Results.Ok(items);
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        return Results.Problem(ex.Message);
    }
})
.WithName("GetAllBenchmarkItems")
.WithOpenApi();

// GET item by id
benchmarkItems.MapGet("/{id}", async (int id, BenchmarkDbContext db, ActivitySource activitySource) =>
{
    using var activity = activitySource.StartActivity("GetBenchmarkItemById", ActivityKind.Server);
    activity?.SetTag("benchmark.item.id", id);
    
    try
    {
        var item = await db.BenchmarkItems.FindAsync(id);
        if (item == null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Item not found");
            return Results.NotFound($"Item with ID {id} not found");
        }
        
        return Results.Ok(item);
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        return Results.Problem(ex.Message);
    }
})
.WithName("GetBenchmarkItemById")
.WithOpenApi();

// POST new item
benchmarkItems.MapPost("/", async (BenchmarkItem item, BenchmarkDbContext db, ActivitySource activitySource) =>
{
    using var activity = activitySource.StartActivity("CreateBenchmarkItem", ActivityKind.Server);
    activity?.SetTag("benchmark.item.name", item.Name);
    
    try
    {
        db.BenchmarkItems.Add(item);
        await db.SaveChangesAsync();
        
        activity?.SetTag("benchmark.item.id", item.Id);
        return Results.Created($"/api/benchmark-items/{item.Id}", item);
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        return Results.Problem(ex.Message);
    }
})
.WithName("CreateBenchmarkItem")
.WithOpenApi();

// PUT update item
benchmarkItems.MapPut("/{id}", async (int id, BenchmarkItem itemUpdate, BenchmarkDbContext db, ActivitySource activitySource) =>
{
    using var activity = activitySource.StartActivity("UpdateBenchmarkItem", ActivityKind.Server);
    activity?.SetTag("benchmark.item.id", id);
    
    try
    {
        var item = await db.BenchmarkItems.FindAsync(id);
        if (item == null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Item not found");
            return Results.NotFound($"Item with ID {id} not found");
        }

        // Update properties
        item.Name = itemUpdate.Name;
        item.Description = itemUpdate.Description;
        item.NumberValue = itemUpdate.NumberValue;
        item.DecimalValue = itemUpdate.DecimalValue;
        
        await db.SaveChangesAsync();
        
        return Results.Ok(item);
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        return Results.Problem(ex.Message);
    }
})
.WithName("UpdateBenchmarkItem")
.WithOpenApi();

// DELETE item
benchmarkItems.MapDelete("/{id}", async (int id, BenchmarkDbContext db, ActivitySource activitySource) =>
{
    using var activity = activitySource.StartActivity("DeleteBenchmarkItem", ActivityKind.Server);
    activity?.SetTag("benchmark.item.id", id);
    
    try
    {
        var item = await db.BenchmarkItems.FindAsync(id);
        if (item == null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Item not found");
            return Results.NotFound($"Item with ID {id} not found");
        }

        db.BenchmarkItems.Remove(item);
        await db.SaveChangesAsync();
        
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        return Results.Problem(ex.Message);
    }
})
.WithName("DeleteBenchmarkItem")
.WithOpenApi();

app.Run();