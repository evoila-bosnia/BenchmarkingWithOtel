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
        
        var totalStopwatch = Stopwatch.StartNew();
        
        try
        {
            activity?.SetTag("benchmark.operation", "get_all");
            _logger.LogInformationWithContext("Fetching all benchmark items");
            
            using var requestPrepActivity = ActivitySource.StartActivity(
                "PrepareRequest", 
                ActivityKind.Internal,
                activity?.Context ?? default);
                
            var requestPrepStopwatch = Stopwatch.StartNew();
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/benchmark-items");
            requestPrepStopwatch.Stop();
            
            requestPrepActivity?.SetTag("benchmark.duration_ms", requestPrepStopwatch.ElapsedMilliseconds);
            
            using var networkActivity = ActivitySource.StartActivity(
                "NetworkRequest", 
                ActivityKind.Internal,
                activity?.Context ?? default);
                
            var networkStopwatch = Stopwatch.StartNew();
            var responseTask = _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
            var completedTask = await Task.WhenAny(responseTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                throw new TimeoutException("Request timed out waiting for response headers");
            }
            
            var httpResponse = await responseTask;
            networkStopwatch.Stop();
            
            networkActivity?.SetTag("benchmark.duration_ms", networkStopwatch.ElapsedMilliseconds);
            networkActivity?.SetTag("benchmark.status_code", (int)httpResponse.StatusCode);
            networkActivity?.SetTag("benchmark.content_length", httpResponse.Content.Headers.ContentLength ?? 0);
            
            using var downloadActivity = ActivitySource.StartActivity(
                "DownloadContent", 
                ActivityKind.Internal,
                activity?.Context ?? default);
                
            var downloadStopwatch = Stopwatch.StartNew();
            var contentStream = await httpResponse.Content.ReadAsStreamAsync();
            downloadStopwatch.Stop();
            
            downloadActivity?.SetTag("benchmark.duration_ms", downloadStopwatch.ElapsedMilliseconds);
            
            using var deserializeActivity = ActivitySource.StartActivity(
                "DeserializeResponse", 
                ActivityKind.Internal,
                activity?.Context ?? default);
                
            var deserializeStopwatch = Stopwatch.StartNew();
            var response = await JsonSerializer.DeserializeAsync<IEnumerable<BenchmarkItem>>(
                contentStream, _jsonOptions, cancellationToken: CancellationToken.None);
            deserializeStopwatch.Stop();
            
            deserializeActivity?.SetTag("benchmark.duration_ms", deserializeStopwatch.ElapsedMilliseconds);
            
            using var postProcessActivity = ActivitySource.StartActivity(
                "PostProcess", 
                ActivityKind.Internal,
                activity?.Context ?? default);
                
            var postProcessStopwatch = Stopwatch.StartNew();
            var itemsList = response?.ToList() ?? new List<BenchmarkItem>();
            var itemCount = itemsList.Count;
            postProcessStopwatch.Stop();
            
            postProcessActivity?.SetTag("benchmark.duration_ms", postProcessStopwatch.ElapsedMilliseconds);
            postProcessActivity?.SetTag("benchmark.items_count", itemCount);
            
            totalStopwatch.Stop();
            
            activity?.SetTag("benchmark.items.count", itemCount);
            activity?.SetTag("benchmark.total_duration_ms", totalStopwatch.ElapsedMilliseconds);
            activity?.SetTag("benchmark.request_prep_ms", requestPrepStopwatch.ElapsedMilliseconds);
            activity?.SetTag("benchmark.network_ms", networkStopwatch.ElapsedMilliseconds);
            activity?.SetTag("benchmark.download_ms", downloadStopwatch.ElapsedMilliseconds);
            activity?.SetTag("benchmark.deserialize_ms", deserializeStopwatch.ElapsedMilliseconds);
            activity?.SetTag("benchmark.post_process_ms", postProcessStopwatch.ElapsedMilliseconds);
            
            _logger.LogInformationWithContext(
                "Retrieved {ItemCount} benchmark items in {TotalTime}ms (Network: {NetworkTime}ms, Download: {DownloadTime}ms, Deserialize: {DeserializeTime}ms, PostProcess: {PostProcessTime}ms)", 
                itemCount, 
                totalStopwatch.ElapsedMilliseconds,
                networkStopwatch.ElapsedMilliseconds,
                downloadStopwatch.ElapsedMilliseconds,
                deserializeStopwatch.ElapsedMilliseconds,
                postProcessStopwatch.ElapsedMilliseconds);
            
            return itemsList;
        }
        catch (Exception ex)
        {
            totalStopwatch.Stop();
            activity?.SetTag("benchmark.error", ex.Message);
            activity?.SetTag("benchmark.total_duration_ms", totalStopwatch.ElapsedMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            _logger.LogErrorWithContext(ex, "Error getting all benchmark items");
            return null;
        }
    }

    public async Task<BenchmarkItem?> GetItemByIdAsync(int id)
    {
        using var activity = ActivitySource.StartActivity("GetItemById", ActivityKind.Client,
                                                         new ActivityContext(ActivityTraceId.CreateRandom(), 
                                                                            ActivitySpanId.CreateRandom(), 
                                                                            ActivityTraceFlags.Recorded));
        
        var totalStopwatch = Stopwatch.StartNew();
        
        activity?.SetTag("benchmark.item.id", id);
        activity?.SetTag("benchmark.operation", "get_by_id");
        
        try
        {
            _logger.LogInformationWithContext("Fetching benchmark item with ID {ItemId}", id);
            
            using var networkActivity = ActivitySource.StartActivity(
                "NetworkRequest", 
                ActivityKind.Internal,
                activity?.Context ?? default);
                
            var networkStopwatch = Stopwatch.StartNew();
            var httpResponse = await _httpClient.GetAsync($"/api/benchmark-items/{id}");
            networkStopwatch.Stop();
            
            networkActivity?.SetTag("benchmark.duration_ms", networkStopwatch.ElapsedMilliseconds);
            networkActivity?.SetTag("benchmark.status_code", (int)httpResponse.StatusCode);
            
            using var downloadActivity = ActivitySource.StartActivity(
                "DownloadContent", 
                ActivityKind.Internal,
                activity?.Context ?? default);
                
            var downloadStopwatch = Stopwatch.StartNew();
            var contentStream = await httpResponse.Content.ReadAsStreamAsync();
            downloadStopwatch.Stop();
            
            downloadActivity?.SetTag("benchmark.duration_ms", downloadStopwatch.ElapsedMilliseconds);
            
            using var deserializeActivity = ActivitySource.StartActivity(
                "DeserializeResponse", 
                ActivityKind.Internal,
                activity?.Context ?? default);
                
            var deserializeStopwatch = Stopwatch.StartNew();
            var item = await JsonSerializer.DeserializeAsync<BenchmarkItem>(
                contentStream, _jsonOptions, cancellationToken: CancellationToken.None);
            deserializeStopwatch.Stop();
            
            deserializeActivity?.SetTag("benchmark.duration_ms", deserializeStopwatch.ElapsedMilliseconds);
            
            totalStopwatch.Stop();
            activity?.SetTag("benchmark.total_duration_ms", totalStopwatch.ElapsedMilliseconds);
            activity?.SetTag("benchmark.network_ms", networkStopwatch.ElapsedMilliseconds);
            activity?.SetTag("benchmark.download_ms", downloadStopwatch.ElapsedMilliseconds);
            activity?.SetTag("benchmark.deserialize_ms", deserializeStopwatch.ElapsedMilliseconds);
            
            _logger.LogInformationWithContext("Retrieved benchmark item {ItemId} in {TotalTime}ms", 
                id, totalStopwatch.ElapsedMilliseconds);
            
            return item;
        }
        catch (Exception ex)
        {
            totalStopwatch.Stop();
            activity?.SetTag("benchmark.error", ex.Message);
            activity?.SetTag("benchmark.total_duration_ms", totalStopwatch.ElapsedMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            _logger.LogErrorWithContext(ex, "Error getting benchmark item with ID {Id}", id);
            return null;
        }
    }

    public async Task<BenchmarkItem?> CreateItemAsync(BenchmarkItem item)
    {
        using var activity = ActivitySource.StartActivity("CreateItem", ActivityKind.Client,
                                                         new ActivityContext(ActivityTraceId.CreateRandom(), 
                                                                            ActivitySpanId.CreateRandom(), 
                                                                            ActivityTraceFlags.Recorded));
        
        var totalStopwatch = Stopwatch.StartNew();
        activity?.SetTag("benchmark.item.name", item.Name);
        activity?.SetTag("benchmark.operation", "create");
        
        try
        {
            _logger.LogInformationWithContext("Creating new benchmark item");
            
            using var networkActivity = ActivitySource.StartActivity(
                "NetworkRequest", 
                ActivityKind.Internal,
                activity?.Context ?? default);
                
            var networkStopwatch = Stopwatch.StartNew();
            var response = await _httpClient.PostAsJsonAsync("/api/benchmark-items", item, _jsonOptions);
            response.EnsureSuccessStatusCode();
            networkStopwatch.Stop();
            
            networkActivity?.SetTag("benchmark.duration_ms", networkStopwatch.ElapsedMilliseconds);
            
            using var deserializeActivity = ActivitySource.StartActivity(
                "DeserializeResponse", 
                ActivityKind.Internal,
                activity?.Context ?? default);
            
            var deserializeStopwatch = Stopwatch.StartNew();
            var createdItem = await response.Content.ReadFromJsonAsync<BenchmarkItem>(_jsonOptions);
            deserializeStopwatch.Stop();
            
            deserializeActivity?.SetTag("benchmark.duration_ms", deserializeStopwatch.ElapsedMilliseconds);
            
            totalStopwatch.Stop();
            activity?.SetTag("benchmark.item.id", createdItem?.Id);
            activity?.SetTag("benchmark.total_duration_ms", totalStopwatch.ElapsedMilliseconds);
            
            _logger.LogInformationWithContext("Created benchmark item with ID {ItemId} in {TotalTime}ms", 
                createdItem?.Id, totalStopwatch.ElapsedMilliseconds);
            
            return createdItem;
        }
        catch (Exception ex)
        {
            totalStopwatch.Stop();
            activity?.SetTag("benchmark.error", ex.Message);
            activity?.SetTag("benchmark.total_duration_ms", totalStopwatch.ElapsedMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            _logger.LogErrorWithContext(ex, "Error creating benchmark item");
            return null;
        }
    }

    public async Task<bool> UpdateItemAsync(int id, BenchmarkItem item)
    {
        using var activity = ActivitySource.StartActivity("UpdateItem", ActivityKind.Client,
                                                         new ActivityContext(ActivityTraceId.CreateRandom(), 
                                                                            ActivitySpanId.CreateRandom(), 
                                                                            ActivityTraceFlags.Recorded));
        
        var totalStopwatch = Stopwatch.StartNew();
        activity?.SetTag("benchmark.item.id", id);
        activity?.SetTag("benchmark.operation", "update");
        
        try
        {
            _logger.LogInformationWithContext("Updating benchmark item with ID {ItemId}", id);
            
            using var networkActivity = ActivitySource.StartActivity(
                "NetworkRequest", 
                ActivityKind.Internal,
                activity?.Context ?? default);
                
            var networkStopwatch = Stopwatch.StartNew();
            var response = await _httpClient.PutAsJsonAsync($"/api/benchmark-items/{id}", item, _jsonOptions);
            response.EnsureSuccessStatusCode();
            networkStopwatch.Stop();
            
            networkActivity?.SetTag("benchmark.duration_ms", networkStopwatch.ElapsedMilliseconds);
            
            totalStopwatch.Stop();
            activity?.SetTag("benchmark.total_duration_ms", totalStopwatch.ElapsedMilliseconds);
            
            _logger.LogInformationWithContext("Successfully updated benchmark item {ItemId} in {TotalTime}ms", 
                id, totalStopwatch.ElapsedMilliseconds);
                
            return true;
        }
        catch (Exception ex)
        {
            totalStopwatch.Stop();
            activity?.SetTag("benchmark.error", ex.Message);
            activity?.SetTag("benchmark.total_duration_ms", totalStopwatch.ElapsedMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            _logger.LogErrorWithContext(ex, "Error updating benchmark item with ID {Id}", id);
            return false;
        }
    }

    public async Task<bool> DeleteItemAsync(int id)
    {
        using var activity = ActivitySource.StartActivity("DeleteItem", ActivityKind.Client,
                                                         new ActivityContext(ActivityTraceId.CreateRandom(), 
                                                                            ActivitySpanId.CreateRandom(), 
                                                                            ActivityTraceFlags.Recorded));
        
        var totalStopwatch = Stopwatch.StartNew();
        activity?.SetTag("benchmark.item.id", id);
        activity?.SetTag("benchmark.operation", "delete");
        
        try
        {
            _logger.LogInformationWithContext("Deleting benchmark item with ID {ItemId}", id);
            
            using var networkActivity = ActivitySource.StartActivity(
                "NetworkRequest", 
                ActivityKind.Internal,
                activity?.Context ?? default);
                
            var networkStopwatch = Stopwatch.StartNew();
            var response = await _httpClient.DeleteAsync($"/api/benchmark-items/{id}");
            response.EnsureSuccessStatusCode();
            networkStopwatch.Stop();
            
            networkActivity?.SetTag("benchmark.duration_ms", networkStopwatch.ElapsedMilliseconds);
            
            totalStopwatch.Stop();
            activity?.SetTag("benchmark.total_duration_ms", totalStopwatch.ElapsedMilliseconds);
            
            _logger.LogInformationWithContext("Successfully deleted benchmark item {ItemId} in {TotalTime}ms", 
                id, totalStopwatch.ElapsedMilliseconds);
                
            return true;
        }
        catch (Exception ex)
        {
            totalStopwatch.Stop();
            activity?.SetTag("benchmark.error", ex.Message);
            activity?.SetTag("benchmark.total_duration_ms", totalStopwatch.ElapsedMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            _logger.LogErrorWithContext(ex, "Error deleting benchmark item with ID {Id}", id);
            return false;
        }
    }
} 