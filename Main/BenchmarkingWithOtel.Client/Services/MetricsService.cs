using System.Diagnostics.Metrics;
using System.Diagnostics;

namespace BenchmarkingWithOtel.Client.Services;

public class MetricsService
{
    private readonly Meter _meter;
    private readonly Counter<long> _operationCounter;
    private readonly Counter<long> _successCounter;
    private readonly Counter<long> _failureCounter;
    private readonly Histogram<double> _latencyHistogram;

    public MetricsService()
    {
        _meter = new Meter("BenchmarkingWithOtel.Client.Metrics");
        
        // Operation counters
        _operationCounter = _meter.CreateCounter<long>(
            "benchmark_operations_total",
            "Operations",
            "Total number of benchmark operations executed");
            
        _successCounter = _meter.CreateCounter<long>(
            "benchmark_operations_success_total",
            "Operations",
            "Total number of successful benchmark operations");
            
        _failureCounter = _meter.CreateCounter<long>(
            "benchmark_operations_failure_total",
            "Operations",
            "Total number of failed benchmark operations");
            
        // Latency metrics
        _latencyHistogram = _meter.CreateHistogram<double>(
            "benchmark_operation_duration_seconds",
            "Seconds",
            "Duration of benchmark operations in seconds");
    }
    
    // Record a completed operation
    public void RecordOperation(string operationType, string result, double durationSeconds)
    {
        var tags = GetTagsWithTraceContext(operationType, result);
        
        _operationCounter.Add(1, tags);
        
        if (result == "success")
        {
            _successCounter.Add(1, tags);
        }
        else
        {
            _failureCounter.Add(1, tags);
        }
        
        var latencyTags = GetTagsWithTraceContext(operationType);
        _latencyHistogram.Record(durationSeconds, latencyTags);
    }
    
    // Record a batch of operations
    public void RecordOperationBatch(string operationType, long totalCount, long successCount, double totalDurationSeconds)
    {
        var basicTags = GetTagsWithTraceContext(operationType);
        _operationCounter.Add(totalCount, basicTags);
        
        var successTags = GetTagsWithTraceContext(operationType, "success");
        _successCounter.Add(successCount, successTags);
        
        if (totalCount > successCount)
        {
            var failureTags = GetTagsWithTraceContext(operationType, "failure");
            _failureCounter.Add(totalCount - successCount, failureTags);
        }
        
        // Record average latency
        if (totalCount > 0)
        {
            double avgDuration = totalDurationSeconds / totalCount;
            _latencyHistogram.Record(avgDuration, basicTags);
        }
    }
    
    // Helper to add trace context to tags
    private KeyValuePair<string, object?>[] GetTagsWithTraceContext(string operationType, string? result = null)
    {
        var activity = Activity.Current;
        var tags = new List<KeyValuePair<string, object?>>();
        
        // Add operation type tag
        tags.Add(new KeyValuePair<string, object?>("operation_type", operationType));
        
        // Add result tag if provided
        if (result != null)
        {
            tags.Add(new KeyValuePair<string, object?>("result", result));
        }
        
        // Add trace context if available
        if (activity != null)
        {
            tags.Add(new KeyValuePair<string, object?>("trace_id", activity.TraceId.ToString()));
            tags.Add(new KeyValuePair<string, object?>("span_id", activity.SpanId.ToString()));
        }
        
        return tags.ToArray();
    }
} 