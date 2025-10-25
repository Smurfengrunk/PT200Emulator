using Serilog;
using System.Diagnostics;
using System.Text;

namespace Logging
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

        public static void LogStackTrace(this object caller, string message = "Stacktrace", bool includeDotNet = false, int maxFrames = 20)
        {
            var category = caller.GetType().Name;
            var stack = CallOriginTracker.GetCallStack(includeDotNet, maxFrames);
            Log.Logger.LogDebug($"{message}:\n{stack}");
        }
    }

    public static class CallOriginTracker
    {
        public static string GetCallStack(bool includeDotNet = false, int maxFrames = 20)
        {
            var trace = new StackTrace(true);
            var sb = new StringBuilder();
            int count = 0;

            foreach (var frame in trace.GetFrames())
            {
                var method = frame.GetMethod();
                var type = method?.DeclaringType?.FullName ?? "<global>";
                var file = frame.GetFileName();
                var line = frame.GetFileLineNumber();

                if (!includeDotNet && type.StartsWith("System."))
                    continue;

                sb.AppendLine($"↪ {type}.{method?.Name} @ {file}:{line}");

                if (++count >= maxFrames)
                    break;
            }

            return sb.ToString();
        }
    }
}
