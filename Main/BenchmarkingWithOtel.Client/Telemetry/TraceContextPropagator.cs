using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace BenchmarkingWithOtel.Client.Telemetry;

/// <summary>
/// Helper class to connect trace context to logs
/// </summary>
public static class TraceContextPropagator
{
    /// <summary>
    /// Logs information with current trace context
    /// </summary>
    public static void LogWithContext(this ILogger logger, LogLevel logLevel, string message, params object[] args)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            using (logger.BeginScope(new Dictionary<string, object>
            {
                ["TraceId"] = activity.TraceId.ToString(),
                ["SpanId"] = activity.SpanId.ToString(),
                ["ParentSpanId"] = activity.ParentSpanId.ToString()
            }))
            {
                logger.Log(logLevel, message, args);
            }
        }
        else
        {
            logger.Log(logLevel, message, args);
        }
    }

    /// <summary>
    /// Logs information with current trace context
    /// </summary>
    public static void LogInformationWithContext(this ILogger logger, string message, params object[] args)
    {
        logger.LogWithContext(LogLevel.Information, message, args);
    }

    /// <summary>
    /// Logs warning with current trace context
    /// </summary>
    public static void LogWarningWithContext(this ILogger logger, string message, params object[] args)
    {
        logger.LogWithContext(LogLevel.Warning, message, args);
    }

    /// <summary>
    /// Logs error with current trace context
    /// </summary>
    public static void LogErrorWithContext(this ILogger logger, string message, params object[] args)
    {
        logger.LogWithContext(LogLevel.Error, message, args);
    }

    /// <summary>
    /// Logs error with exception and current trace context
    /// </summary>
    public static void LogErrorWithContext(this ILogger logger, Exception exception, string message, params object[] args)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            using (logger.BeginScope(new Dictionary<string, object>
            {
                ["TraceId"] = activity.TraceId.ToString(),
                ["SpanId"] = activity.SpanId.ToString(),
                ["ParentSpanId"] = activity.ParentSpanId.ToString()
            }))
            {
                logger.LogError(exception, message, args);
            }
        }
        else
        {
            logger.LogError(exception, message, args);
        }
    }
} 