using System;
using System.IO;
using System.Reflection;

namespace InstaShit.Bot
{
    public enum LogType
    {
        Communication,
        Queue
    }
    public static class Log
    {
        private static readonly string assemblyLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

        private static readonly object _lock = new object();

        public static void Write(string text, LogType logType)
        {
            if (logType == LogType.Communication)
            {
                lock(_lock)
                {
                    using (StreamWriter file = new StreamWriter(Path.Combine(assemblyLocation, "communication.log"), true))
                        file.WriteLine(DateTime.UtcNow + ": " + text);
                }
            }
            else if (logType == LogType.Queue)
            {
                using(StreamWriter file = new StreamWriter(Path.Combine(assemblyLocation, "queue.log"), true))
                    file.WriteLine(DateTime.UtcNow + ": " + text);
            }
        }
    }
}
