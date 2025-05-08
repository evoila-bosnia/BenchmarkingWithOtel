using BenchmarkingWithOtel.Client.Models;
using BenchmarkingWithOtel.Client.Telemetry;
using System.Diagnostics;

namespace BenchmarkingWithOtel.Client.Services;

public class BenchmarkRunner
{
    private readonly BenchmarkService _benchmarkService;
    private readonly ILogger<BenchmarkRunner> _logger;
    private readonly MetricsService _metricsService;
    private static readonly ActivitySource ActivitySource = new("BenchmarkingWithOtel.Client.Runner");
    
    private readonly Random _random = new();

    public BenchmarkRunner(BenchmarkService benchmarkService, ILogger<BenchmarkRunner> logger, MetricsService metricsService)
    {
        _benchmarkService = benchmarkService;
        _logger = logger;
        _metricsService = metricsService;
    }

    public async Task<BenchmarkResult> RunCreateBenchmarkAsync(int count, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("RunCreateBenchmark", ActivityKind.Internal);
        activity?.AddTag("benchmark.operation_count", count);
        
        _logger.LogInformationWithContext("Starting CREATE benchmark with {Count} items", count);
        var stopwatch = Stopwatch.StartNew();
        var successCount = 0;
        var progressStep = Math.Max(1, count / 20); // Show progress roughly every 5%

        for (int i = 0; i < count && !cancellationToken.IsCancellationRequested; i++)
        {
            var operationStopwatch = Stopwatch.StartNew();
            var item = GenerateRandomItem();
            var result = await _benchmarkService.CreateItemAsync(item);
            operationStopwatch.Stop();
            
            string operationResult = result != null ? "success" : "failure";
            _metricsService.RecordOperation("create", operationResult, operationStopwatch.Elapsed.TotalSeconds);
            
            if (result != null)
            {
                successCount++;
            }
            
            // Show progress
            if (i > 0 && i % progressStep == 0)
            {
                var percentComplete = (int)((double)i / count * 100);
                _logger.LogInformationWithContext("CREATE progress: {PercentComplete}% ({CurrentCount}/{TotalCount})", 
                    percentComplete, i, count);
            }
        }

        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var requestsPerSecond = successCount / (elapsedMs / 1000.0);

        activity?.AddTag("benchmark.success_count", successCount);
        activity?.AddTag("benchmark.elapsed_ms", elapsedMs);
        activity?.AddTag("benchmark.requests_per_second", requestsPerSecond);
        
        _logger.LogInformationWithContext("CREATE benchmark completed: {Success}/{Total} operations in {ElapsedMs}ms ({RequestsPerSecond:F2} req/sec)",
            successCount, count, elapsedMs, requestsPerSecond);
            
        // Record batch metrics
        _metricsService.RecordOperationBatch("create", count, successCount, stopwatch.Elapsed.TotalSeconds);
            
        return new BenchmarkResult
        {
            Name = "CREATE",
            TotalOperations = count,
            SuccessCount = successCount,
            ElapsedMilliseconds = elapsedMs
        };
    }

    public async Task<BenchmarkResult> RunReadBenchmarkAsync(int count, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("RunReadBenchmark", ActivityKind.Internal);
        activity?.AddTag("benchmark.operation_count", count);
        
        // First, get all available IDs
        var allItems = await _benchmarkService.GetAllItemsAsync();
        if (allItems == null || !allItems.Any())
        {
            _logger.LogWarningWithContext("No items found in database to run READ benchmark");
            activity?.SetStatus(ActivityStatusCode.Error, "No items to benchmark");
            
            return new BenchmarkResult 
            { 
                Name = "READ",
                TotalOperations = 0,
                SuccessCount = 0,
                ElapsedMilliseconds = 0
            };
        }

        var ids = allItems.Select(i => i.Id).ToList();
        _logger.LogInformationWithContext("Starting READ benchmark with {Count} operations", count);
        
        var stopwatch = Stopwatch.StartNew();
        var successCount = 0;
        var progressStep = Math.Max(1, count / 20); // Show progress roughly every 5%

        for (int i = 0; i < count && !cancellationToken.IsCancellationRequested; i++)
        {
            // Randomly select an ID
            var id = ids[_random.Next(ids.Count)];
            
            var operationStopwatch = Stopwatch.StartNew();
            var item = await _benchmarkService.GetItemByIdAsync(id);
            operationStopwatch.Stop();
            
            string operationResult = item != null ? "success" : "failure";
            _metricsService.RecordOperation("read", operationResult, operationStopwatch.Elapsed.TotalSeconds);
            
            if (item != null)
            {
                successCount++;
            }
            
            // Show progress
            if (i > 0 && i % progressStep == 0)
            {
                var percentComplete = (int)((double)i / count * 100);
                _logger.LogInformationWithContext("READ progress: {PercentComplete}% ({CurrentCount}/{TotalCount})", 
                    percentComplete, i, count);
            }
        }

        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var requestsPerSecond = successCount / (elapsedMs / 1000.0);
        
        activity?.AddTag("benchmark.success_count", successCount);
        activity?.AddTag("benchmark.elapsed_ms", elapsedMs);
        activity?.AddTag("benchmark.requests_per_second", requestsPerSecond);

        _logger.LogInformationWithContext("READ benchmark completed: {Success}/{Total} operations in {ElapsedMs}ms ({RequestsPerSecond:F2} req/sec)",
            successCount, count, elapsedMs, requestsPerSecond);
            
        // Record batch metrics
        _metricsService.RecordOperationBatch("read", count, successCount, stopwatch.Elapsed.TotalSeconds);
            
        return new BenchmarkResult
        {
            Name = "READ",
            TotalOperations = count,
            SuccessCount = successCount,
            ElapsedMilliseconds = elapsedMs
        };
    }

    public async Task<BenchmarkResult> RunUpdateBenchmarkAsync(int count, CancellationToken cancellationToken)
    {
        // First, get all available IDs
        var allItems = await _benchmarkService.GetAllItemsAsync();
        if (allItems == null || !allItems.Any())
        {
            _logger.LogWarning("No items found in database to run UPDATE benchmark");
            return new BenchmarkResult 
            { 
                Name = "UPDATE",
                TotalOperations = 0,
                SuccessCount = 0,
                ElapsedMilliseconds = 0
            };
        }

        var itemsList = allItems.ToList();
        _logger.LogInformation("Starting UPDATE benchmark with {Count} operations", count);
        
        var stopwatch = Stopwatch.StartNew();
        var successCount = 0;
        var progressStep = Math.Max(1, count / 20); // Show progress roughly every 5%

        for (int i = 0; i < count && !cancellationToken.IsCancellationRequested; i++)
        {
            // Randomly select an item
            var item = itemsList[_random.Next(itemsList.Count)];
            
            // Modify the item with random values
            item.Name = $"Updated-{Guid.NewGuid().ToString()[..8]}";
            item.Description = $"Updated description {DateTime.UtcNow}";
            item.NumberValue = _random.Next(1000);
            item.DecimalValue = _random.NextDouble() * 1000;
            
            var operationStopwatch = Stopwatch.StartNew();
            var success = await _benchmarkService.UpdateItemAsync(item.Id, item);
            operationStopwatch.Stop();
            
            string operationResult = success ? "success" : "failure";
            _metricsService.RecordOperation("update", operationResult, operationStopwatch.Elapsed.TotalSeconds);
            
            if (success)
            {
                successCount++;
            }
            
            // Show progress
            if (i > 0 && i % progressStep == 0)
            {
                var percentComplete = (int)((double)i / count * 100);
                _logger.LogInformation("UPDATE progress: {PercentComplete}% ({CurrentCount}/{TotalCount})", 
                    percentComplete, i, count);
            }
        }

        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var requestsPerSecond = successCount / (elapsedMs / 1000.0);

        _logger.LogInformation("UPDATE benchmark completed: {Success}/{Total} operations in {ElapsedMs}ms ({RequestsPerSecond:F2} req/sec)",
            successCount, count, elapsedMs, requestsPerSecond);
            
        // Record batch metrics
        _metricsService.RecordOperationBatch("update", count, successCount, stopwatch.Elapsed.TotalSeconds);
            
        return new BenchmarkResult
        {
            Name = "UPDATE",
            TotalOperations = count,
            SuccessCount = successCount,
            ElapsedMilliseconds = elapsedMs
        };
    }

    public async Task<BenchmarkResult> RunDeleteBenchmarkAsync(int count, CancellationToken cancellationToken)
    {
        // First create items to delete
        _logger.LogInformation("Creating {Count} items for DELETE benchmark", count);
        var createdItems = new List<BenchmarkItem>();
        var progressStep = Math.Max(1, count / 10); // Show progress for item creation
        
        for (int i = 0; i < count && !cancellationToken.IsCancellationRequested; i++)
        {
            var item = GenerateRandomItem();
            var result = await _benchmarkService.CreateItemAsync(item);
            
            if (result != null)
            {
                createdItems.Add(result);
            }
            
            // Show progress for creation
            if (i > 0 && i % progressStep == 0)
            {
                var percentComplete = (int)((double)i / count * 100);
                _logger.LogInformation("DELETE setup progress: {PercentComplete}% ({CurrentCount}/{TotalCount})", 
                    percentComplete, i, count);
            }
        }

        if (!createdItems.Any())
        {
            _logger.LogWarning("Failed to create items for DELETE benchmark");
            return new BenchmarkResult 
            { 
                Name = "DELETE",
                TotalOperations = 0,
                SuccessCount = 0,
                ElapsedMilliseconds = 0
            };
        }

        _logger.LogInformation("Starting DELETE benchmark with {Count} operations", createdItems.Count);
        
        var stopwatch = Stopwatch.StartNew();
        var successCount = 0;
        var deleteCount = createdItems.Count;
        progressStep = Math.Max(1, deleteCount / 10); // Show progress for deletion

        for (int i = 0; i < deleteCount && i < createdItems.Count && !cancellationToken.IsCancellationRequested; i++)
        {
            var item = createdItems[i];
            
            var operationStopwatch = Stopwatch.StartNew();
            var success = await _benchmarkService.DeleteItemAsync(item.Id);
            operationStopwatch.Stop();
            
            string operationResult = success ? "success" : "failure";
            _metricsService.RecordOperation("delete", operationResult, operationStopwatch.Elapsed.TotalSeconds);
            
            if (success)
            {
                successCount++;
            }
            
            // Show progress for deletion
            if (i > 0 && i % progressStep == 0)
            {
                var percentComplete = (int)((double)i / deleteCount * 100);
                _logger.LogInformation("DELETE progress: {PercentComplete}% ({CurrentCount}/{TotalCount})", 
                    percentComplete, i, deleteCount);
            }
        }

        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var requestsPerSecond = successCount / (elapsedMs / 1000.0);

        _logger.LogInformation("DELETE benchmark completed: {Success}/{Total} operations in {ElapsedMs}ms ({RequestsPerSecond:F2} req/sec)",
            successCount, deleteCount, elapsedMs, requestsPerSecond);
            
        // Record batch metrics
        _metricsService.RecordOperationBatch("delete", deleteCount, successCount, stopwatch.Elapsed.TotalSeconds);
            
        return new BenchmarkResult
        {
            Name = "DELETE",
            TotalOperations = deleteCount,
            SuccessCount = successCount,
            ElapsedMilliseconds = elapsedMs
        };
    }

    public async Task<BenchmarkResult> RunMixedWorkloadBenchmarkAsync(int count, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting MIXED workload benchmark with {Count} operations", count);
        
        // Create initial data
        var initialCount = Math.Min(count / 10, 100);
        _logger.LogInformation("Creating {Count} initial items for MIXED workload", initialCount);
        
        for (int i = 0; i < initialCount && !cancellationToken.IsCancellationRequested; i++)
        {
            await _benchmarkService.CreateItemAsync(GenerateRandomItem());
        }
        
        var stopwatch = Stopwatch.StartNew();
        var successCount = 0;
        var createCount = 0;
        var readCount = 0;
        var updateCount = 0;
        var deleteCount = 0;
        var progressStep = Math.Max(1, count / 20); // Show progress roughly every 5%

        // Get initial IDs
        var allItems = (await _benchmarkService.GetAllItemsAsync())?.ToList() ?? new List<BenchmarkItem>();
        
        for (int i = 0; i < count && !cancellationToken.IsCancellationRequested; i++)
        {
            // Randomly choose operation: 20% create, 60% read, 15% update, 5% delete
            var operation = _random.Next(100);
            var operationStopwatch = Stopwatch.StartNew();
            var success = false;
            var operationType = "";
            
            if (operation < 20)
            {
                // CREATE
                operationType = "create";
                var newItem = GenerateRandomItem();
                var result = await _benchmarkService.CreateItemAsync(newItem);
                if (result != null)
                {
                    allItems.Add(result);
                    successCount++;
                    createCount++;
                    success = true;
                }
            }
            else if (operation < 80)
            {
                // READ
                operationType = "read";
                if (allItems.Any())
                {
                    var id = allItems[_random.Next(allItems.Count)].Id;
                    var item = await _benchmarkService.GetItemByIdAsync(id);
                    if (item != null)
                    {
                        successCount++;
                        readCount++;
                        success = true;
                    }
                }
            }
            else if (operation < 95)
            {
                // UPDATE
                operationType = "update";
                if (allItems.Any())
                {
                    var item = allItems[_random.Next(allItems.Count)];
                    item.Name = $"Mixed-{Guid.NewGuid().ToString()[..8]}";
                    item.NumberValue = _random.Next(1000);
                    
                    var result = await _benchmarkService.UpdateItemAsync(item.Id, item);
                    if (result)
                    {
                        successCount++;
                        updateCount++;
                        success = true;
                    }
                }
            }
            else
            {
                // DELETE
                operationType = "delete";
                if (allItems.Any())
                {
                    var index = _random.Next(allItems.Count);
                    var item = allItems[index];
                    
                    var result = await _benchmarkService.DeleteItemAsync(item.Id);
                    if (result)
                    {
                        allItems.RemoveAt(index);
                        successCount++;
                        deleteCount++;
                        success = true;
                    }
                }
            }
            
            operationStopwatch.Stop();
            string operationResult = success ? "success" : "failure";
            _metricsService.RecordOperation("mixed_" + operationType, operationResult, operationStopwatch.Elapsed.TotalSeconds);
            
            // Show progress
            if (i > 0 && i % progressStep == 0)
            {
                var percentComplete = (int)((double)i / count * 100);
                _logger.LogInformation("MIXED workload progress: {PercentComplete}% ({CurrentCount}/{TotalCount})", 
                    percentComplete, i, count);
            }
        }

        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var requestsPerSecond = successCount / (elapsedMs / 1000.0);

        _logger.LogInformation("MIXED workload benchmark completed: {Success}/{Total} operations in {ElapsedMs}ms ({RequestsPerSecond:F2} req/sec)",
            successCount, count, elapsedMs, requestsPerSecond);
        
        _logger.LogInformation("MIXED workload details: CREATE={CreateCount}, READ={ReadCount}, UPDATE={UpdateCount}, DELETE={DeleteCount}",
            createCount, readCount, updateCount, deleteCount);
            
        // Record batch metrics
        _metricsService.RecordOperationBatch("mixed", count, successCount, stopwatch.Elapsed.TotalSeconds);
        _metricsService.RecordOperationBatch("mixed_create", createCount, createCount, 0); // All successful
        _metricsService.RecordOperationBatch("mixed_read", readCount, readCount, 0); // All successful
        _metricsService.RecordOperationBatch("mixed_update", updateCount, updateCount, 0); // All successful
        _metricsService.RecordOperationBatch("mixed_delete", deleteCount, deleteCount, 0); // All successful
            
        return new BenchmarkResult
        {
            Name = "MIXED",
            TotalOperations = count,
            SuccessCount = successCount,
            ElapsedMilliseconds = elapsedMs,
            CreateCount = createCount,
            ReadCount = readCount,
            UpdateCount = updateCount,
            DeleteCount = deleteCount
        };
    }

    private BenchmarkItem GenerateRandomItem()
    {
        return new BenchmarkItem
        {
            Name = $"Benchmark-{Guid.NewGuid().ToString()[..8]}",
            Description = $"Benchmark item created at {DateTime.UtcNow}",
            NumberValue = _random.Next(1000),
            DecimalValue = _random.NextDouble() * 1000,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
} 