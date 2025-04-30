using BenchmarkingWithOtel.Client.Models;
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
        using var activity = ActivitySource.StartActivity("GetAllItems", ActivityKind.Client);
        
        try
        {
            var response = await _httpClient.GetFromJsonAsync<IEnumerable<BenchmarkItem>>("/api/benchmark-items", _jsonOptions);
            activity?.SetTag("benchmark.items.count", response?.Count() ?? 0);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all benchmark items");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return null;
        }
    }

    public async Task<BenchmarkItem?> GetItemByIdAsync(int id)
    {
        using var activity = ActivitySource.StartActivity("GetItemById", ActivityKind.Client);
        activity?.SetTag("benchmark.item.id", id);
        
        try
        {
            return await _httpClient.GetFromJsonAsync<BenchmarkItem>($"/api/benchmark-items/{id}", _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting benchmark item with ID {Id}", id);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return null;
        }
    }

    public async Task<BenchmarkItem?> CreateItemAsync(BenchmarkItem item)
    {
        using var activity = ActivitySource.StartActivity("CreateItem", ActivityKind.Client);
        activity?.SetTag("benchmark.item.name", item.Name);
        
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/benchmark-items", item, _jsonOptions);
            response.EnsureSuccessStatusCode();
            
            var createdItem = await response.Content.ReadFromJsonAsync<BenchmarkItem>(_jsonOptions);
            activity?.SetTag("benchmark.item.id", createdItem?.Id);
            return createdItem;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating benchmark item");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return null;
        }
    }

    public async Task<bool> UpdateItemAsync(int id, BenchmarkItem item)
    {
        using var activity = ActivitySource.StartActivity("UpdateItem", ActivityKind.Client);
        activity?.SetTag("benchmark.item.id", id);
        
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/benchmark-items/{id}", item, _jsonOptions);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating benchmark item with ID {Id}", id);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return false;
        }
    }

    public async Task<bool> DeleteItemAsync(int id)
    {
        using var activity = ActivitySource.StartActivity("DeleteItem", ActivityKind.Client);
        activity?.SetTag("benchmark.item.id", id);
        
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/benchmark-items/{id}");
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting benchmark item with ID {Id}", id);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return false;
        }
    }
} 