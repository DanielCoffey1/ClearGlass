using System;
using System.IO;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace ClearGlass.Services
{
    public class LoggingService
    {
        private readonly ILogger _logger;
        private readonly string _logFilePath;
        private static LoggingService? _instance;
        private int _stepCounter = 0;

        /// <summary>
        /// Gets the singleton instance of the LoggingService
        /// </summary>
        public static LoggingService Instance => _instance ??= new LoggingService();

        public LoggingService()
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ClearGlass",
                "Logs"
            );

            Directory.CreateDirectory(logDirectory);

            _logFilePath = Path.Combine(
                logDirectory,
                $"ClearGlass_{DateTime.Now:yyyy-MM-dd}.log"
            );

            var serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(_logFilePath,
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            var factory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddSerilog(serilogLogger)
                    .SetMinimumLevel(LogLevel.Debug);
            });

            _logger = factory.CreateLogger<LoggingService>();
            _instance = this;

            LogSectionHeader("CLEAR GLASS APPLICATION STARTED");
            LogInformation("Log file location: {LogPath}", _logFilePath);
        }

        public void LogInformation(string messageTemplate, params object[] args)
        {
            _logger.LogInformation(messageTemplate, args);
        }

        public void LogWarning(string messageTemplate, params object[] args)
        {
            _logger.LogWarning(messageTemplate, args);
        }

        public void LogError(string messageTemplate, Exception? exception = null)
        {
            if (exception != null)
            {
                _logger.LogError(exception, messageTemplate);
            }
            else
            {
                _logger.LogError(messageTemplate);
            }
        }

        public void LogError(string messageTemplate, params object[] args)
        {
            _logger.LogError(messageTemplate, args);
        }

        public void LogAppRemoval(string appName, bool success, string? errorMessage = null)
        {
            if (success)
            {
                LogInformation("Successfully removed app: {AppName}", appName);
            }
            else
            {
                LogError("Failed to remove app: {AppName}. Error: {Error}", appName, errorMessage ?? "Unknown error");
            }
        }

        public void LogOperationStart(string operation)
        {
            LogInformation("Starting operation: {Operation}", operation);
        }

        public void LogOperationComplete(string operation)
        {
            LogInformation("Completed operation: {Operation}", operation);
        }

        public string GetCurrentLogPath() => _logFilePath;

        // ═══════════════════════════════════════════════════════════════════
        // Beautiful Formatting Methods
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Logs a major section header with decorative borders
        /// </summary>
        public void LogSectionHeader(string title)
        {
            var border = new string('═', 60);
            _logger.LogInformation("");
            _logger.LogInformation(border);
            _logger.LogInformation("  {Title}", title.ToUpper());
            _logger.LogInformation(border);
        }

        /// <summary>
        /// Logs a subsection header
        /// </summary>
        public void LogSubsection(string title)
        {
            var border = new string('─', 50);
            _logger.LogInformation("");
            _logger.LogInformation(border);
            _logger.LogInformation("  {Title}", title);
            _logger.LogInformation(border);
        }

        /// <summary>
        /// Logs the start of a numbered step
        /// </summary>
        public void LogStep(string stepDescription)
        {
            _stepCounter++;
            _logger.LogInformation("");
            _logger.LogInformation("▶ Step {StepNumber}: {Description}", _stepCounter, stepDescription);
        }

        /// <summary>
        /// Logs a step with a custom number
        /// </summary>
        public void LogStep(int stepNumber, int totalSteps, string stepDescription)
        {
            _logger.LogInformation("");
            _logger.LogInformation("▶ Step {StepNumber}/{TotalSteps}: {Description}", stepNumber, totalSteps, stepDescription);
        }

        /// <summary>
        /// Resets the step counter (call at the start of a new operation)
        /// </summary>
        public void ResetStepCounter()
        {
            _stepCounter = 0;
        }

        /// <summary>
        /// Logs a successful action with a checkmark
        /// </summary>
        public void LogSuccess(string message, params object[] args)
        {
            _logger.LogInformation("  ✓ " + message, args);
        }

        /// <summary>
        /// Logs a failed action with an X mark
        /// </summary>
        public void LogFailure(string message, params object[] args)
        {
            _logger.LogWarning("  ✗ " + message, args);
        }

        /// <summary>
        /// Logs a detail/info line with indentation
        /// </summary>
        public void LogDetail(string message, params object[] args)
        {
            _logger.LogInformation("    → {Message}", string.Format(message.Replace("{", "{{").Replace("}", "}}"), args));
        }

        /// <summary>
        /// Logs a debug-level detail
        /// </summary>
        public void LogDebugDetail(string message, params object[] args)
        {
            _logger.LogDebug("    · " + message, args);
        }

        /// <summary>
        /// Logs the start of a retry attempt
        /// </summary>
        public void LogRetry(int attempt, int maxAttempts, string operation)
        {
            _logger.LogWarning("  ↻ Retry {Attempt}/{Max}: {Operation}", attempt, maxAttempts, operation);
        }

        /// <summary>
        /// Logs a waiting/progress message
        /// </summary>
        public void LogWaiting(string message)
        {
            _logger.LogInformation("  ⏳ {Message}", message);
        }

        /// <summary>
        /// Logs the completion of an operation with timing
        /// </summary>
        public void LogOperationComplete(string operation, TimeSpan duration)
        {
            _logger.LogInformation("  ✓ {Operation} completed in {Duration:F2}s", operation, duration.TotalSeconds);
        }

        /// <summary>
        /// Logs a registry operation
        /// </summary>
        public void LogRegistry(string action, string keyPath, string valueName, object? value = null)
        {
            if (value != null)
            {
                _logger.LogInformation("    [REG] {Action}: {Key}\\{Name} = {Value}", action, keyPath, valueName, value);
            }
            else
            {
                _logger.LogInformation("    [REG] {Action}: {Key}\\{Name}", action, keyPath, valueName);
            }
        }

        /// <summary>
        /// Logs a Windows API operation
        /// </summary>
        public void LogWinApi(string operation, string details)
        {
            _logger.LogInformation("    [API] {Operation}: {Details}", operation, details);
        }

        /// <summary>
        /// Logs a process operation
        /// </summary>
        public void LogProcess(string action, string processName, string? details = null)
        {
            if (details != null)
            {
                _logger.LogInformation("    [PROC] {Action} {Process}: {Details}", action, processName, details);
            }
            else
            {
                _logger.LogInformation("    [PROC] {Action} {Process}", action, processName);
            }
        }

        /// <summary>
        /// Logs a summary box at the end of an operation
        /// </summary>
        public void LogSummary(string title, int successCount, int failureCount, TimeSpan? totalDuration = null)
        {
            _logger.LogInformation("");
            _logger.LogInformation("┌─────────────────────────────────────────────────┐");
            _logger.LogInformation("│  {Title,-45} │", title.ToUpper());
            _logger.LogInformation("├─────────────────────────────────────────────────┤");
            _logger.LogInformation("│  Successful: {Success,-5}  Failed: {Failed,-5}           │", successCount, failureCount);
            if (totalDuration.HasValue)
            {
                _logger.LogInformation("│  Total time: {Duration:F2}s                            │", totalDuration.Value.TotalSeconds);
            }
            _logger.LogInformation("└─────────────────────────────────────────────────┘");
        }
    }
} 