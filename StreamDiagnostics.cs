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

        public static void Initialise()
        {
            lock (Sync)
            {
                if (FileListener != null)
                    return;

                var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                Directory.CreateDirectory(directory);
                FileListener = new TextWriterTraceListener(Path.Combine(directory, "stream-diagnostics.log"));
                Trace.Listeners.Add(FileListener);
                Trace.AutoFlush = true;
                Write("diagnostics.initialised");
            }
        }

        public static void Write(string message)
        {
            lock (Sync)
            {
                Trace.WriteLine($"{DateTime.UtcNow:O} {message}");
                Trace.Flush();
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
