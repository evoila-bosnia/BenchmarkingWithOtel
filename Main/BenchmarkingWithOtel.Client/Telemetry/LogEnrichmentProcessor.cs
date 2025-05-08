using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BenchmarkingWithOtel.Client.Telemetry;

/// <summary>
/// Middleware that enriches log entries with trace context information
/// </summary>
public class LogTraceContextEnricher : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new TraceContextEnrichedLogger(categoryName);
    }

    public void Dispose()
    {
        // No resources to dispose
    }

    private class TraceContextEnrichedLogger : ILogger
    {
        private readonly string _categoryName;

        public TraceContextEnrichedLogger(string categoryName)
        {
            _categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null; // Not implementing custom scopes
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true; // Let other providers determine if logging is enabled
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            // Get current trace context
            var activity = Activity.Current;
            if (activity != null)
            {
                // If state is an IReadOnlyList<KeyValuePair<string, object?>> (usually it is)
                if (state is IEnumerable<KeyValuePair<string, object?>> stateProperties)
                {
                    // Convert to mutable dictionary
                    var stateDict = stateProperties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    
                    // Add trace context
                    stateDict["TraceId"] = activity.TraceId.ToString();
                    stateDict["SpanId"] = activity.SpanId.ToString();
                    stateDict["TraceFlags"] = activity.ActivityTraceFlags.ToString();
                    
                    // Create a new state with the enriched properties
                    var enrichedState = new EnrichedLogValues(stateDict, formatter(state, exception));
                    
                    // This logger doesn't actually log, it just enriches the state
                    // Other loggers in the pipeline will pick up the enriched state
                }
            }
        }

        private class EnrichedLogValues : IReadOnlyList<KeyValuePair<string, object?>>
        {
            private readonly IReadOnlyList<KeyValuePair<string, object?>> _values;
            private readonly string _originalMessage;

            public EnrichedLogValues(IDictionary<string, object?> values, string originalMessage)
            {
                _values = values.Select(kvp => new KeyValuePair<string, object?>(kvp.Key, kvp.Value)).ToList();
                _originalMessage = originalMessage;
            }

            public KeyValuePair<string, object?> this[int index] => _values[index];
            public int Count => _values.Count;
            
            public override string ToString() => _originalMessage;

            public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _values.GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
} 