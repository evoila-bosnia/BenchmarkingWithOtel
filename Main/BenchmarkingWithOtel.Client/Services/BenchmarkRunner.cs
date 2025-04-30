using BenchmarkingWithOtel.Client.Models;
using System.Diagnostics;

namespace BenchmarkingWithOtel.Client.Services;

public class BenchmarkRunner
{
    private readonly BenchmarkService _benchmarkService;
    private readonly ILogger<BenchmarkRunner> _logger;
    
    private readonly Random _random = new();

    public BenchmarkRunner(BenchmarkService benchmarkService, ILogger<BenchmarkRunner> logger)
    {
        _benchmarkService = benchmarkService;
        _logger = logger;
    }

    public async Task RunCreateBenchmarkAsync(int count, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting CREATE benchmark with {Count} items", count);
        var stopwatch = Stopwatch.StartNew();
        var successCount = 0;

        for (int i = 0; i < count && !cancellationToken.IsCancellationRequested; i++)
        {
            var item = GenerateRandomItem();
            var result = await _benchmarkService.CreateItemAsync(item);
            
            if (result != null)
            {
                successCount++;
            }
        }

        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var requestsPerSecond = successCount / (elapsedMs / 1000.0);

        _logger.LogInformation("CREATE benchmark completed: {Success}/{Total} operations in {ElapsedMs}ms ({RequestsPerSecond:F2} req/sec)",
            successCount, count, elapsedMs, requestsPerSecond);
    }

    public async Task RunReadBenchmarkAsync(int count, CancellationToken cancellationToken)
    {
        // First, get all available IDs
        var allItems = await _benchmarkService.GetAllItemsAsync();
        if (allItems == null || !allItems.Any())
        {
            _logger.LogWarning("No items found in database to run READ benchmark");
            return;
        }

        var ids = allItems.Select(i => i.Id).ToList();
        _logger.LogInformation("Starting READ benchmark with {Count} operations", count);
        
        var stopwatch = Stopwatch.StartNew();
        var successCount = 0;

        for (int i = 0; i < count && !cancellationToken.IsCancellationRequested; i++)
        {
            // Randomly select an ID
            var id = ids[_random.Next(ids.Count)];
            var item = await _benchmarkService.GetItemByIdAsync(id);
            
            if (item != null)
            {
                successCount++;
            }
        }

        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var requestsPerSecond = successCount / (elapsedMs / 1000.0);

        _logger.LogInformation("READ benchmark completed: {Success}/{Total} operations in {ElapsedMs}ms ({RequestsPerSecond:F2} req/sec)",
            successCount, count, elapsedMs, requestsPerSecond);
    }

    public async Task RunUpdateBenchmarkAsync(int count, CancellationToken cancellationToken)
    {
        // First, get all available IDs
        var allItems = await _benchmarkService.GetAllItemsAsync();
        if (allItems == null || !allItems.Any())
        {
            _logger.LogWarning("No items found in database to run UPDATE benchmark");
            return;
        }

        var itemsList = allItems.ToList();
        _logger.LogInformation("Starting UPDATE benchmark with {Count} operations", count);
        
        var stopwatch = Stopwatch.StartNew();
        var successCount = 0;

        for (int i = 0; i < count && !cancellationToken.IsCancellationRequested; i++)
        {
            // Randomly select an item
            var item = itemsList[_random.Next(itemsList.Count)];
            
            // Modify the item with random values
            item.Name = $"Updated-{Guid.NewGuid().ToString()[..8]}";
            item.Description = $"Updated description {DateTime.UtcNow}";
            item.NumberValue = _random.Next(1000);
            item.DecimalValue = _random.NextDouble() * 1000;
            
            var success = await _benchmarkService.UpdateItemAsync(item.Id, item);
            if (success)
            {
                successCount++;
            }
        }

        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var requestsPerSecond = successCount / (elapsedMs / 1000.0);

        _logger.LogInformation("UPDATE benchmark completed: {Success}/{Total} operations in {ElapsedMs}ms ({RequestsPerSecond:F2} req/sec)",
            successCount, count, elapsedMs, requestsPerSecond);
    }

    public async Task RunDeleteBenchmarkAsync(int count, CancellationToken cancellationToken)
    {
        // First create items to delete
        _logger.LogInformation("Creating {Count} items for DELETE benchmark", count);
        var createdItems = new List<BenchmarkItem>();
        
        for (int i = 0; i < count && !cancellationToken.IsCancellationRequested; i++)
        {
            var item = GenerateRandomItem();
            var result = await _benchmarkService.CreateItemAsync(item);
            
            if (result != null)
            {
                createdItems.Add(result);
            }
        }

        if (!createdItems.Any())
        {
            _logger.LogWarning("Failed to create items for DELETE benchmark");
            return;
        }

        _logger.LogInformation("Starting DELETE benchmark with {Count} operations", createdItems.Count);
        
        var stopwatch = Stopwatch.StartNew();
        var successCount = 0;

        foreach (var item in createdItems)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
                
            var success = await _benchmarkService.DeleteItemAsync(item.Id);
            if (success)
            {
                successCount++;
            }
        }

        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var requestsPerSecond = successCount / (elapsedMs / 1000.0);

        _logger.LogInformation("DELETE benchmark completed: {Success}/{Total} operations in {ElapsedMs}ms ({RequestsPerSecond:F2} req/sec)",
            successCount, createdItems.Count, elapsedMs, requestsPerSecond);
    }

    public async Task RunMixedWorkloadBenchmarkAsync(int count, CancellationToken cancellationToken)
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

        // Get initial IDs
        var allItems = (await _benchmarkService.GetAllItemsAsync())?.ToList() ?? new List<BenchmarkItem>();
        
        for (int i = 0; i < count && !cancellationToken.IsCancellationRequested; i++)
        {
            // Randomly choose operation: 20% create, 60% read, 15% update, 5% delete
            var operation = _random.Next(100);
            
            if (operation < 20)
            {
                // CREATE
                var newItem = GenerateRandomItem();
                var result = await _benchmarkService.CreateItemAsync(newItem);
                if (result != null)
                {
                    allItems.Add(result);
                    successCount++;
                    createCount++;
                }
            }
            else if (operation < 80)
            {
                // READ
                if (allItems.Any())
                {
                    var id = allItems[_random.Next(allItems.Count)].Id;
                    var item = await _benchmarkService.GetItemByIdAsync(id);
                    if (item != null)
                    {
                        successCount++;
                        readCount++;
                    }
                }
            }
            else if (operation < 95)
            {
                // UPDATE
                if (allItems.Any())
                {
                    var item = allItems[_random.Next(allItems.Count)];
                    item.Name = $"Mixed-{Guid.NewGuid().ToString()[..8]}";
                    item.NumberValue = _random.Next(1000);
                    
                    var success = await _benchmarkService.UpdateItemAsync(item.Id, item);
                    if (success)
                    {
                        successCount++;
                        updateCount++;
                    }
                }
            }
            else
            {
                // DELETE
                if (allItems.Any())
                {
                    var index = _random.Next(allItems.Count);
                    var item = allItems[index];
                    
                    var success = await _benchmarkService.DeleteItemAsync(item.Id);
                    if (success)
                    {
                        allItems.RemoveAt(index);
                        successCount++;
                        deleteCount++;
                    }
                }
            }
        }

        stopwatch.Stop();
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var requestsPerSecond = successCount / (elapsedMs / 1000.0);

        _logger.LogInformation("MIXED workload benchmark completed: {Success}/{Total} operations in {ElapsedMs}ms ({RequestsPerSecond:F2} req/sec)",
            successCount, count, elapsedMs, requestsPerSecond);
        
        _logger.LogInformation("MIXED workload details: CREATE={CreateCount}, READ={ReadCount}, UPDATE={UpdateCount}, DELETE={DeleteCount}",
            createCount, readCount, updateCount, deleteCount);
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