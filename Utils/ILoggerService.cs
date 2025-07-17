using Serilog.Events;
using System;

namespace Utils
{
    public interface ILoggerService
    {
        string LogDirectoryPath { get; }
        void SetMinimumLogLevel(LogEventLevel level);
        void LogVerbose(string messageTemplate, params object[] propertyValues);
        void LogDebug(string messageTemplate, params object[] propertyValues);
        void LogInfo(string messageTemplate, params object[] propertyValues);
        void LogWarning(string messageTemplate, params object[] propertyValues);
        void LogError(string messageTemplate, params object[] propertyValues);
        void LogError(Exception exception, string messageTemplate, params object[] propertyValues);
        void LogFatal(string messageTemplate, params object[] propertyValues);
        void LogFatal(Exception exception, string messageTemplate, params object[] propertyValues);
    }
}
