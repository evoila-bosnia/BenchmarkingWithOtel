using System.Text;

namespace BenchmarkingWithOtel.Client.Models;

public class BenchmarkResult
{
    public string Name { get; set; } = string.Empty;
    public int TotalOperations { get; set; }
    public int SuccessCount { get; set; }
    public long ElapsedMilliseconds { get; set; }
    public double RequestsPerSecond => SuccessCount / (ElapsedMilliseconds / 1000.0);
    
    // Additional data for mixed workload
    public int? CreateCount { get; set; }
    public int? ReadCount { get; set; }
    public int? UpdateCount { get; set; }
    public int? DeleteCount { get; set; }
    
    public static string GenerateSummary(List<BenchmarkResult> results)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("\n=================================================");
        sb.AppendLine("            BENCHMARK SUMMARY REPORT             ");
        sb.AppendLine("=================================================");
        
        var totalTime = results.Sum(r => r.ElapsedMilliseconds);
        var totalOperations = results.Sum(r => r.TotalOperations);
        var totalSuccessful = results.Sum(r => r.SuccessCount);
        var overallRps = totalSuccessful / (totalTime / 1000.0);
        
        sb.AppendLine($"Total time: {totalTime / 1000.0:F2} seconds");
        sb.AppendLine($"Total operations: {totalOperations}");
        sb.AppendLine($"Successful operations: {totalSuccessful} ({(double)totalSuccessful / totalOperations:P2})");
        sb.AppendLine($"Average throughput: {overallRps:F2} requests/second");
        sb.AppendLine("=================================================");
        sb.AppendLine("BENCHMARK RESULTS BY OPERATION TYPE:");
        
        foreach (var result in results)
        {
            sb.AppendLine($"\n{result.Name}:");
            sb.AppendLine($"  Operations: {result.TotalOperations}");
            sb.AppendLine($"  Successful: {result.SuccessCount} ({(double)result.SuccessCount / result.TotalOperations:P2})");
            sb.AppendLine($"  Time: {result.ElapsedMilliseconds / 1000.0:F2} seconds");
            sb.AppendLine($"  Throughput: {result.RequestsPerSecond:F2} requests/second");
            
            if (result.Name == "MIXED" && result.CreateCount.HasValue)
            {
                sb.AppendLine($"  Create operations: {result.CreateCount}");
                sb.AppendLine($"  Read operations: {result.ReadCount}");
                sb.AppendLine($"  Update operations: {result.UpdateCount}");
                sb.AppendLine($"  Delete operations: {result.DeleteCount}");
            }
        }
        
        sb.AppendLine("\n=================================================");
        return sb.ToString();
    }
} 