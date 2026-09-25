using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace WSServer
{
    internal static class StreamDiagnostics
    {
        private static readonly object Sync = new object();
        private static TextWriterTraceListener FileListener;
        private static string LogPath;

        public static void Initialise()
        {
            lock (Sync)
            {
                if (FileListener != null)
                    return;

                var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                Directory.CreateDirectory(directory);
                LogPath = Path.Combine(directory, "stream-diagnostics.log");
                FileListener = new TextWriterTraceListener(LogPath);
                Trace.Listeners.Add(FileListener);
                Trace.AutoFlush = true;
                Write("diagnostics.initialised");
            }
        }

        public static void Write(string message)
        {
            lock (Sync)
            {
                var line = $"{DateTime.UtcNow:O} {message}{Environment.NewLine}";
                if (LogPath != null)
                    File.AppendAllText(LogPath, line);
                Debug.WriteLine(line.TrimEnd());
            }
        }

        public static void ConnectionStatus(object statusEvent)
        {
            if (statusEvent == null)
            {
                Write("betfair.connection_status event=null");
                return;
            }

            var values = statusEvent.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanRead && !p.Name.ToLowerInvariant().Contains("token") &&
                            !p.Name.ToLowerInvariant().Contains("password") &&
                            !p.Name.ToLowerInvariant().Contains("secret"))
                .Select(p =>
                {
                    try { return $"{p.Name}={p.GetValue(statusEvent, null)}"; }
                    catch { return $"{p.Name}=<unavailable>"; }
                });

            Write("betfair.connection_status " + string.Join(" ", values));
        }
    }
}
