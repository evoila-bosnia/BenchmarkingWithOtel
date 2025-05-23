namespace BenchmarkingWithOtel.Server.Models;

public class BenchmarkItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int NumberValue { get; set; }
    public double DecimalValue { get; set; }
} 