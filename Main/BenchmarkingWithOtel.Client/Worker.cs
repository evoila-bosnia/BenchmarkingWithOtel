using BenchmarkingWithOtel.Client.Models;
using BenchmarkingWithOtel.Client.Services;
using BenchmarkingWithOtel.Client.Telemetry;
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
        // Clear any current activity to prevent creating a parent trace
        Activity.Current = null;
        
        // Allow some time for the server to start
        _logger.LogInformationWithContext("Waiting for server to start...");
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        
        // Get benchmark configuration
        var operationCount = _configuration.GetValue<int>("Benchmark:OperationCount", 1000);
        var iterations = _configuration.GetValue<int>("Benchmark:Iterations", 3);
        var delayBetweenRuns = _configuration.GetValue<int>("Benchmark:DelayBetweenRuns", 5000);
        
        _logger.LogInformationWithContext("Starting benchmark with {OperationCount} operations per test, {Iterations} iterations", 
            operationCount, iterations);
        
        var allResults = new List<List<BenchmarkResult>>();
        
        for (int i = 0; i < iterations && !stoppingToken.IsCancellationRequested; i++)
        {
            // Reset current activity for each iteration
            Activity.Current = null;
            
            _logger.LogInformationWithContext("Starting iteration {Iteration}/{TotalIterations}", i + 1, iterations);
            var iterationResults = new List<BenchmarkResult>();
            
            // CREATE benchmark
            var createResult = await _benchmarkRunner.RunCreateBenchmarkAsync(operationCount, stoppingToken);
            iterationResults.Add(createResult);
            await Task.Delay(delayBetweenRuns, stoppingToken);
            
            // READ benchmark
            var readResult = await _benchmarkRunner.RunReadBenchmarkAsync(operationCount, stoppingToken);
            iterationResults.Add(readResult);
            await Task.Delay(delayBetweenRuns, stoppingToken);
            
            // UPDATE benchmark
            var updateResult = await _benchmarkRunner.RunUpdateBenchmarkAsync(operationCount, stoppingToken);
            iterationResults.Add(updateResult);
            await Task.Delay(delayBetweenRuns, stoppingToken);
            
            // DELETE benchmark
            var deleteResult = await _benchmarkRunner.RunDeleteBenchmarkAsync(operationCount / 10, stoppingToken);
            iterationResults.Add(deleteResult);
            await Task.Delay(delayBetweenRuns, stoppingToken);
            
            // MIXED workload benchmark
            var mixedResult = await _benchmarkRunner.RunMixedWorkloadBenchmarkAsync(operationCount, stoppingToken);
            iterationResults.Add(mixedResult);
            
            allResults.Add(iterationResults);
            
            if (i < iterations - 1)
            {
                _logger.LogInformationWithContext("Iteration {Iteration} completed. Waiting for {Delay}ms before next iteration", 
                    i + 1, delayBetweenRuns * 2);
                await Task.Delay(delayBetweenRuns * 2, stoppingToken);
            }
            
            // Print iteration summary
            var iterationSummary = BenchmarkResult.GenerateSummary(iterationResults);
            _logger.LogInformationWithContext("Iteration {Iteration}/{TotalIterations} Summary:\n{Summary}", 
                i + 1, iterations, iterationSummary);
        }
        
        // Generate final summary across all iterations
        if (allResults.Count > 0)
        {
            _logger.LogInformationWithContext("Benchmark completed. Generating final summary...");
            
            // Combine results across iterations for each benchmark type
            var finalResults = new List<BenchmarkResult>();
            
            // Get benchmark types from the first iteration
            var benchmarkTypes = allResults[0].Select(r => r.Name).ToList();
            
            foreach (var benchmarkType in benchmarkTypes)
            {
                // Get all results of this type across iterations
                var benchmarkResults = allResults.SelectMany(r => r.Where(br => br.Name == benchmarkType)).ToList();
                
                // Create a combined result
                var combinedResult = new BenchmarkResult
                {
                    Name = $"{benchmarkType} (Average across {allResults.Count} iterations)",
                    TotalOperations = benchmarkResults.Sum(r => r.TotalOperations),
                    SuccessCount = benchmarkResults.Sum(r => r.SuccessCount),
                    ElapsedMilliseconds = benchmarkResults.Sum(r => r.ElapsedMilliseconds)
                };
                
                // Add MIXED workload details if present
                if (benchmarkType == "MIXED" && benchmarkResults.All(r => r.CreateCount.HasValue))
                {
                    combinedResult.CreateCount = benchmarkResults.Sum(r => r.CreateCount);
                    combinedResult.ReadCount = benchmarkResults.Sum(r => r.ReadCount);
                    combinedResult.UpdateCount = benchmarkResults.Sum(r => r.UpdateCount);
                    combinedResult.DeleteCount = benchmarkResults.Sum(r => r.DeleteCount);
                }
                
                finalResults.Add(combinedResult);
            }
            
            var finalSummary = BenchmarkResult.GenerateSummary(finalResults);
            _logger.LogInformationWithContext("FINAL BENCHMARK SUMMARY (Across all iterations):\n{Summary}", finalSummary);
        }
        else
        {
            _logger.LogWarningWithContext("Benchmark completed but no results were collected");
        }
    }
}
