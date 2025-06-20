using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using System.IO;

namespace Utils
{
    /// <summary>
    /// Provides a static logging service using Serilog.
    /// This service is configured to log to a file and the debug output.
    /// </summary>
    public static class LogService
    {
        private static readonly LoggingLevelSwitch _levelSwitch = new LoggingLevelSwitch();
        private static readonly string _logFilePath; // Store for access if needed

        /// <summary>
        /// Initializes the LogService.
        /// Configures Serilog to write to a file in the application's roaming data folder
        /// and to the debug console.
        /// The log file path is: %APPDATA%\Assistant\logsYYYYMMDD.txt (daily rolling)
        /// The output template includes Timestamp, LogLevel, Message, and Exception details.
        /// </summary>
        static LogService()
        {
            // Choice of Serilog:
            // Serilog is a popular, well-documented, and highly configurable logging library for .NET.
            // It supports structured logging, various sinks (outputs), and is widely adopted in the community,
            // aligning with the request for using Microsoft and community standards.

            // Log file location strategy:
            // Logs are stored in Environment.SpecialFolder.ApplicationData (typically C:\\Users\\username\\AppData\\Roaming).
            // This is a standard location for application-specific user data that should roam with the user profile.
            // A dedicated "Assistant" subfolder is used to keep logs organized.
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string logDirectory = Path.Combine(appDataPath, "Assistant");
            // The actual file name will be logsYYYYMMDD.txt due to rollingInterval: RollingInterval.Day
            // _logFilePath will store the pattern or the directory for clarity.
            _logFilePath = Path.Combine(logDirectory, "logs.txt"); // Base name, Serilog adds date

            // Ensure the directory exists
            Directory.CreateDirectory(logDirectory);

            // Configuration:
            // - MinimumLevel.ControlledBy(_levelSwitch): Allows dynamic changing of log level. Defaulting to Verbose.
            // - Enrich.FromLogContext(): Enables context-based enrichment (e.g., adding properties within a scope).
            // - WriteTo.Debug(): Writes logs to the Visual Studio Debug output window.
            // - WriteTo.File(): Writes logs to a file.
            //   - path: _logFilePath specifies the base path pattern for the log file.
            //   - outputTemplate: Defines the format: Timestamp, Level, Message, NewLine, Exception.
            //   - rollingInterval: RollingInterval.Day creates a new log file each day (e.g., logs20231027.txt).
            //   - retainedFileCountLimit: Keeps the last 7 daily log files.
            //   - fileSizeLimitBytes: Limits each log file to 10MB.
            //   - rollOnFileSizeLimit: If a daily log exceeds 10MB, it will roll into another file (e.g., logs20231027_001.txt).
            _levelSwitch.MinimumLevel = LogEventLevel.Verbose; // Default log level

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(_levelSwitch)
                .Enrich.FromLogContext()
                .WriteTo.Debug(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(_logFilePath, // Serilog handles the YYYYMMDD in the filename based on rollingInterval
                              outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                              rollingInterval: RollingInterval.Day,
                              retainedFileCountLimit: 7,
                              fileSizeLimitBytes: 10 * 1024 * 1024, // 10 MB
                              rollOnFileSizeLimit: true)
                .CreateLogger();

            Log.Information("Logging service initialized. Log base path: {LogBasePath}", _logFilePath);
        }

        /// <summary>
        /// Gets the directory where log files are stored.
        /// </summary>
        public static string LogDirectoryPath => Path.GetDirectoryName(_logFilePath);

        /// <summary>
        /// Sets the minimum logging level dynamically.
        /// </summary>
        /// <param name="level">The minimum LogEventLevel to set.</param>
        public static void SetMinimumLogLevel(LogEventLevel level)
        {
            _levelSwitch.MinimumLevel = level;
            Log.Information("Minimum log level set to {LogLevel}", level);
        }

        /// <summary>
        /// Logs a message with the Verbose level.
        /// </summary>
        /// <param name="messageTemplate">Message template describing the event.</param>
        /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
        public static void LogVerbose(string messageTemplate, params object[] propertyValues)
        {
            Log.Verbose(messageTemplate, propertyValues);
        }

        /// <summary>
        /// Logs a message with the Debug level.
        /// </summary>
        /// <param name="messageTemplate">Message template describing the event.</param>
        /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
        public static void LogDebug(string messageTemplate, params object[] propertyValues)
        {
            Log.Debug(messageTemplate, propertyValues);
        }

        /// <summary>
        /// Logs a message with the Information level.
        /// </summary>
        /// <param name="messageTemplate">Message template describing the event.</param>
        /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
        public static void LogInfo(string messageTemplate, params object[] propertyValues)
        {
            Log.Information(messageTemplate, propertyValues);
        }

        /// <summary>
        /// Logs a message with the Warning level.
        /// </summary>
        /// <param name="messageTemplate">Message template describing the event.</param>
        /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
        public static void LogWarning(string messageTemplate, params object[] propertyValues)
        {
            Log.Warning(messageTemplate, propertyValues);
        }

        /// <summary>
        /// Logs an error message with an optional exception.
        /// </summary>
        /// <param name="messageTemplate">Message template describing the event.</param>
        /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
        public static void LogError(string messageTemplate, params object[] propertyValues)
        {
            Log.Error(messageTemplate, propertyValues);
        }

        /// <summary>
        /// Logs an error message with an exception.
        /// </summary>
        /// <param name="exception">The exception related to the error.</param>
        /// <param name="messageTemplate">Message template describing the event.</param>
        /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
        public static void LogError(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            Log.Error(exception, messageTemplate, propertyValues);
        }

        /// <summary>
        /// Logs a message with the Fatal level.
        /// </summary>
        /// <param name="messageTemplate">Message template describing the event.</param>
        /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
        public static void LogFatal(string messageTemplate, params object[] propertyValues)
        {
            Log.Fatal(messageTemplate, propertyValues);
        }

        /// <summary>
        /// Logs a fatal error message with an exception.
        /// </summary>
        /// <param name="exception">The exception related to the fatal error.</param>
        /// <param name="messageTemplate">Message template describing the event.</param>
        /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
        public static void LogFatal(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            Log.Fatal(exception, messageTemplate, propertyValues);
        }
    }
}
