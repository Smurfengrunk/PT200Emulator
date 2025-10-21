using Serilog;

namespace Parser
{
    public static class LogExtensions
    {
        public static void LogDebug(this object _, string message, params object[] args)
            => Log.Debug(message, args);

        public static void LogInformation(this object _, string message, params object[] args)
            => Log.Information(message, args);

        public static void LogWarning(this object _, string message, params object[] args)
            => Log.Warning(message, args);

        public static void LogErr(this object _, string message, params object[] args)
            => Log.Error(message, args);

        public static void LogTrace(this object _, string message, params object[] args)
            => Log.Verbose(message, args);
    }
}
