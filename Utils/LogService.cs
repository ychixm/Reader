using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using System.IO;

namespace Utils
{
    /// <summary>
    /// Provides a logging service using Serilog.
    /// This service is configured to log to a file and the debug output.
    /// </summary>
    public class LogService : ILoggerService
    {
        private readonly Serilog.ILogger _logger;
        private readonly Serilog.Core.LoggingLevelSwitch _levelSwitch; // Changed from _globalLevelSwitch and made non-nullable

        // Constructor now accepts LoggingLevelSwitch
        public LogService(Serilog.Core.LoggingLevelSwitch levelSwitch)
        {
            _logger = Serilog.Log.Logger; // Assumes Serilog.Log.Logger is configured globally
            _levelSwitch = levelSwitch ?? throw new ArgumentNullException(nameof(levelSwitch));
            _logger.Debug("LogService instance created, using globally configured Serilog.Log.Logger and provided LoggingLevelSwitch.");
        }

        /// <summary>
        /// Gets the directory where log files are stored.
        /// </summary>
        public string LogDirectoryPath
        {
            get
            {
                // This is a bit of a hack as Serilog's ILogger doesn't directly expose sink paths.
                // For this specific app, we know the path structure from App.xaml.cs.
                // A more robust solution would involve custom Serilog configuration access if this path is critical.
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appDataPath, "Assistant");
            }
        }

        /// <summary>
        /// Sets the minimum logging level dynamically using the global switch.
        /// </summary>
        /// <param name="level">The minimum LogEventLevel to set.</param>
        public void SetMinimumLogLevel(LogEventLevel level)
        {
            // Uses the injected levelSwitch directly
            _levelSwitch.MinimumLevel = level;
            _logger.Information("Minimum log level set to {LogLevel}", level);
        }

        /// <summary>
        /// Logs a message with the Verbose level.
        /// </summary>
        /// <param name="messageTemplate">Message template describing the event.</param>
        /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
        public void LogVerbose(string messageTemplate, params object[] propertyValues)
        {
            _logger.Verbose(messageTemplate, propertyValues);
        }

        /// <summary>
        /// Logs a message with the Debug level.
        /// </summary>
        /// <param name="messageTemplate">Message template describing the event.</param>
        /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
        public void LogDebug(string messageTemplate, params object[] propertyValues)
        {
            _logger.Debug(messageTemplate, propertyValues);
        }

        /// <summary>
        /// Logs a message with the Information level.
        /// </summary>
        /// <param name="messageTemplate">Message template describing the event.</param>
        /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
        public void LogInfo(string messageTemplate, params object[] propertyValues)
        {
            _logger.Information(messageTemplate, propertyValues);
        }

        /// <summary>
        /// Logs a message with the Warning level.
        /// </summary>
        /// <param name="messageTemplate">Message template describing the event.</param>
        /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
        public void LogWarning(string messageTemplate, params object[] propertyValues)
        {
            _logger.Warning(messageTemplate, propertyValues);
        }

        /// <summary>
        /// Logs an error message with an optional exception.
        /// </summary>
        /// <param name="messageTemplate">Message template describing the event.</param>
        /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
        public void LogError(string messageTemplate, params object[] propertyValues)
        {
            _logger.Error(messageTemplate, propertyValues);
        }

        /// <summary>
        /// Logs an error message with an exception.
        /// </summary>
        /// <param name="exception">The exception related to the error.</param>
        /// <param name="messageTemplate">Message template describing the event.</param>
        /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
        public void LogError(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            _logger.Error(exception, messageTemplate, propertyValues);
        }

        /// <summary>
        /// Logs a message with the Fatal level.
        /// </summary>
        /// <param name="messageTemplate">Message template describing the event.</param>
        /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
        public void LogFatal(string messageTemplate, params object[] propertyValues)
        {
            _logger.Fatal(messageTemplate, propertyValues);
        }

        /// <summary>
        /// Logs a fatal error message with an exception.
        /// </summary>
        /// <param name="exception">The exception related to the fatal error.</param>
        /// <param name="messageTemplate">Message template describing the event.</param>
        /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
        public void LogFatal(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            _logger.Fatal(exception, messageTemplate, propertyValues);
        }
    }
}
