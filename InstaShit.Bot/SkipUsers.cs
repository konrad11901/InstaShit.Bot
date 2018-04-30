using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace InstaShit.Bot
{
    public static class SkipUsers
    {
        private static readonly object _lock = new object();
        private static List<string> _SkipUsersList;
        public static List<string> SkipUsersList
        {
            get
            {
                lock (_lock)
                {
                    return _SkipUsersList;
                }
            }
            private set
            {
                lock (_lock)
                {
                    _SkipUsersList = value;
                }
            }
        }
        private static readonly string assemblyLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        public static void Add(string user)
        {
            SkipUsersList.Add(user);
            Write();
        }
        public static void Remove(string user)
        {
            SkipUsersList.Remove(user);
            Write();
        }
        public static void Refresh()
        {
            if (File.Exists(Path.Combine(assemblyLocation, "skipusers.json")))
                SkipUsersList = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(Path.Combine(assemblyLocation, "skipusers.json")));
            else
                SkipUsersList = new List<string>();
        }
        private static void Write()
        {
            File.WriteAllText(Path.Combine(assemblyLocation, "skipusers.json"), JsonConvert.SerializeObject(SkipUsersList, Formatting.Indented));
        }
        static SkipUsers()
        {
            Refresh();
        }
    }
}
