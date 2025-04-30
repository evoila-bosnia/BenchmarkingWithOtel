namespace BenchmarkingWithOtel.Client.Models;

public class BenchmarkItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int NumberValue { get; set; }
    public double DecimalValue { get; set; }
} 