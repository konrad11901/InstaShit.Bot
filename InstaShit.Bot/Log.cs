using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace InstaShit.Bot
{
    public enum LogType
    {
        Communication,
        Queue
    }
    public static class Log
    {
        private static string assemblyLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

        public static void Write(string text, LogType logType)
        {
            if (logType == LogType.Communication)
            {
                StreamWriter file = new StreamWriter(Path.Combine(assemblyLocation, "communication.log"), true);
                file.WriteLine(DateTime.UtcNow + ": " + text);
                file.Close();
            }
            else if (logType == LogType.Queue)
            {
                StreamWriter file = new StreamWriter(Path.Combine(assemblyLocation, "queue.log"), true);
                file.WriteLine(DateTime.UtcNow + ": " + text);
                file.Close();
            }
        }
    }
}
