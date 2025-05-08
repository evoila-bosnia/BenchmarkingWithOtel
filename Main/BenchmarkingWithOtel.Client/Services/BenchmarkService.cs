using BenchmarkingWithOtel.Client.Models;
using BenchmarkingWithOtel.Client.Telemetry;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace BenchmarkingWithOtel.Client.Services;

public class BenchmarkService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BenchmarkService> _logger;
    private static readonly ActivitySource ActivitySource = new("BenchmarkingWithOtel.Client.Benchmark");
    
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public BenchmarkService(HttpClient httpClient, ILogger<BenchmarkService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IEnumerable<BenchmarkItem>?> GetAllItemsAsync()
    {
        using var activity = ActivitySource.StartActivity("GetAllItems", ActivityKind.Client, 
                                                         new ActivityContext(ActivityTraceId.CreateRandom(), 
                                                                            ActivitySpanId.CreateRandom(), 
                                                                            ActivityTraceFlags.Recorded));
        
        try
        {
            activity?.SetTag("benchmark.operation", "get_all");
            _logger.LogInformationWithContext("Fetching all benchmark items");
            
            var response = await _httpClient.GetFromJsonAsync<IEnumerable<BenchmarkItem>>("/api/benchmark-items", _jsonOptions);
            var itemCount = response?.Count() ?? 0;
            
            activity?.SetTag("benchmark.items.count", itemCount);
            _logger.LogInformationWithContext("Retrieved {ItemCount} benchmark items", itemCount);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithContext(ex, "Error getting all benchmark items");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return null;
        }
    }

    public async Task<BenchmarkItem?> GetItemByIdAsync(int id)
    {
        using var activity = ActivitySource.StartActivity("GetItemById", ActivityKind.Client,
                                                         new ActivityContext(ActivityTraceId.CreateRandom(), 
                                                                            ActivitySpanId.CreateRandom(), 
                                                                            ActivityTraceFlags.Recorded));
        
        activity?.SetTag("benchmark.item.id", id);
        activity?.SetTag("benchmark.operation", "get_by_id");
        
        try
        {
            _logger.LogInformationWithContext("Fetching benchmark item with ID {ItemId}", id);
            var item = await _httpClient.GetFromJsonAsync<BenchmarkItem>($"/api/benchmark-items/{id}", _jsonOptions);
            _logger.LogInformationWithContext("Retrieved benchmark item {ItemId}", id);
            return item;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithContext(ex, "Error getting benchmark item with ID {Id}", id);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return null;
        }
    }

    public async Task<BenchmarkItem?> CreateItemAsync(BenchmarkItem item)
    {
        using var activity = ActivitySource.StartActivity("CreateItem", ActivityKind.Client,
                                                         new ActivityContext(ActivityTraceId.CreateRandom(), 
                                                                            ActivitySpanId.CreateRandom(), 
                                                                            ActivityTraceFlags.Recorded));
        
        activity?.SetTag("benchmark.item.name", item.Name);
        activity?.SetTag("benchmark.operation", "create");
        
        try
        {
            _logger.LogInformationWithContext("Creating new benchmark item");
            var response = await _httpClient.PostAsJsonAsync("/api/benchmark-items", item, _jsonOptions);
            response.EnsureSuccessStatusCode();
            
            var createdItem = await response.Content.ReadFromJsonAsync<BenchmarkItem>(_jsonOptions);
            activity?.SetTag("benchmark.item.id", createdItem?.Id);
            _logger.LogInformationWithContext("Created benchmark item with ID {ItemId}", createdItem?.Id);
            
            return createdItem;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithContext(ex, "Error creating benchmark item");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return null;
        }
    }

    public async Task<bool> UpdateItemAsync(int id, BenchmarkItem item)
    {
        using var activity = ActivitySource.StartActivity("UpdateItem", ActivityKind.Client,
                                                         new ActivityContext(ActivityTraceId.CreateRandom(), 
                                                                            ActivitySpanId.CreateRandom(), 
                                                                            ActivityTraceFlags.Recorded));
        
        activity?.SetTag("benchmark.item.id", id);
        activity?.SetTag("benchmark.operation", "update");
        
        try
        {
            _logger.LogInformationWithContext("Updating benchmark item with ID {ItemId}", id);
            var response = await _httpClient.PutAsJsonAsync($"/api/benchmark-items/{id}", item, _jsonOptions);
            response.EnsureSuccessStatusCode();
            _logger.LogInformationWithContext("Successfully updated benchmark item {ItemId}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithContext(ex, "Error updating benchmark item with ID {Id}", id);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return false;
        }
    }

    public async Task<bool> DeleteItemAsync(int id)
    {
        using var activity = ActivitySource.StartActivity("DeleteItem", ActivityKind.Client,
                                                         new ActivityContext(ActivityTraceId.CreateRandom(), 
                                                                            ActivitySpanId.CreateRandom(), 
                                                                            ActivityTraceFlags.Recorded));
        
        activity?.SetTag("benchmark.item.id", id);
        activity?.SetTag("benchmark.operation", "delete");
        
        try
        {
            _logger.LogInformationWithContext("Deleting benchmark item with ID {ItemId}", id);
            var response = await _httpClient.DeleteAsync($"/api/benchmark-items/{id}");
            response.EnsureSuccessStatusCode();
            _logger.LogInformationWithContext("Successfully deleted benchmark item {ItemId}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogErrorWithContext(ex, "Error deleting benchmark item with ID {Id}", id);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return false;
        }
    }
} 