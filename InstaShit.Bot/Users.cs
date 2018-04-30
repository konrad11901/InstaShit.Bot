using System.Collections.Generic;
using System.IO;
using System.Reflection;
using InstaShit.Bot.Models;
using Newtonsoft.Json;

namespace InstaShit.Bot
{
    public static class Users
    {
        private static readonly object _lock = new object();
        private static List<User> _UsersList;
        public static List<User> UsersList {
            get
            {
                lock(_lock)
                {
                    return _UsersList;
                }
            }
            private set
            {
                lock(_lock)
                {
                    _UsersList = value;
                }
            }
        }
        private static readonly string assemblyLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        public static void Add(User user)
        {
            UsersList.Add(user);
            Write();
        }
        public static void Remove(User user)
        {
            UsersList.Remove(user);
            Write();
        }
        public static void Refresh()
        {
            if (File.Exists(Path.Combine(assemblyLocation, "users.json")))
                UsersList = JsonConvert.DeserializeObject<List<User>>(File.ReadAllText(Path.Combine(assemblyLocation, "users.json")));
            else
                UsersList = new List<User>();
        }
        private static void Write()
        {
            File.WriteAllText(Path.Combine(assemblyLocation, "users.json"), JsonConvert.SerializeObject(UsersList, Formatting.Indented));
        }
        static Users()
        {
            Refresh();
        }
    }
}
