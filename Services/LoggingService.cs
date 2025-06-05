using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace ClearGlass.Services
{
    public class LoggingService
    {
        private readonly ILogger _logger;
        private readonly string _logFilePath;

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
                .MinimumLevel.Information()
                .WriteTo.File(_logFilePath, 
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            var factory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddSerilog(serilogLogger)
                    .SetMinimumLevel(LogLevel.Information);
            });

            _logger = factory.CreateLogger<LoggingService>();
            LogInformation("Logging service initialized");
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
    }
} 