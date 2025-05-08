using BenchmarkingWithOtel.Server.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace BenchmarkingWithOtel.Server.Data;

public class BenchmarkDbContext : DbContext
{
    private static readonly ActivitySource ActivitySource = new ActivitySource("BenchmarkingWithOtel.Server.Data");

    public BenchmarkDbContext(DbContextOptions<BenchmarkDbContext> options) : base(options)
    {
        
    }

    public DbSet<BenchmarkItem> BenchmarkItems { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BenchmarkItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
        });
    }
    
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("SaveChanges", ActivityKind.Client);
        activity?.SetTag("db.system", "oracle");
        
        foreach (var entry in ChangeTracker.Entries<BenchmarkItem>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }

        try
        {
            var result = await base.SaveChangesAsync(cancellationToken);
            activity?.SetTag("db.result", result);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }
} 