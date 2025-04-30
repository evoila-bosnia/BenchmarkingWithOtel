using BenchmarkingWithOtel.Client.Services;
using System.Diagnostics;

namespace BenchmarkingWithOtel.Client;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly BenchmarkRunner _benchmarkRunner;
    private readonly IConfiguration _configuration;

    public Worker(ILogger<Worker> logger, BenchmarkRunner benchmarkRunner, IConfiguration configuration)
    {
        _logger = logger;
        _benchmarkRunner = benchmarkRunner;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Allow some time for the server to start
        _logger.LogInformation("Waiting for server to start...");
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        
        // Get benchmark configuration
        var operationCount = _configuration.GetValue<int>("Benchmark:OperationCount", 1000);
        var iterations = _configuration.GetValue<int>("Benchmark:Iterations", 3);
        var delayBetweenRuns = _configuration.GetValue<int>("Benchmark:DelayBetweenRuns", 5000);
        
        _logger.LogInformation("Starting benchmark with {OperationCount} operations per test, {Iterations} iterations", 
            operationCount, iterations);
        
        for (int i = 0; i < iterations && !stoppingToken.IsCancellationRequested; i++)
        {
            _logger.LogInformation("Starting iteration {Iteration}/{TotalIterations}", i + 1, iterations);
            
            // CREATE benchmark
            await _benchmarkRunner.RunCreateBenchmarkAsync(operationCount, stoppingToken);
            await Task.Delay(delayBetweenRuns, stoppingToken);
            
            // READ benchmark
            await _benchmarkRunner.RunReadBenchmarkAsync(operationCount, stoppingToken);
            await Task.Delay(delayBetweenRuns, stoppingToken);
            
            // UPDATE benchmark
            await _benchmarkRunner.RunUpdateBenchmarkAsync(operationCount, stoppingToken);
            await Task.Delay(delayBetweenRuns, stoppingToken);
            
            // DELETE benchmark
            await _benchmarkRunner.RunDeleteBenchmarkAsync(operationCount / 10, stoppingToken);
            await Task.Delay(delayBetweenRuns, stoppingToken);
            
            // MIXED workload benchmark
            await _benchmarkRunner.RunMixedWorkloadBenchmarkAsync(operationCount, stoppingToken);
            
            if (i < iterations - 1)
            {
                _logger.LogInformation("Iteration {Iteration} completed. Waiting for {Delay}ms before next iteration", 
                    i + 1, delayBetweenRuns * 2);
                await Task.Delay(delayBetweenRuns * 2, stoppingToken);
            }
        }
        
        _logger.LogInformation("Benchmark completed");
    }
}
