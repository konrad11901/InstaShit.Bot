using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using InstaShit.Bot.Models;
using Newtonsoft.Json;

namespace InstaShit.Bot
{
    public static class Users
    {
        public static List<User> UsersList { get; private set; }
        private static readonly string assemblyLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        private static readonly object _lock = new object();
        public static void Add(User user)
        {
            lock(_lock)
            {
                UsersList.Add(user);
                Write();
            }
        }
        public static void Remove(User user)
        {
            lock(_lock)
            {
                UsersList.Remove(user);
                Write();
            }
        }
        public static void Refresh()
        {
            lock(_lock)
            {
                if (File.Exists(Path.Combine(assemblyLocation, "users.json")))
                    UsersList = JsonConvert.DeserializeObject<List<User>>(File.ReadAllText(Path.Combine(assemblyLocation, "users.json")));
                else
                    UsersList = new List<User>();
            }
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
