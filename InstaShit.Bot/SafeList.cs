using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace InstaShit.Bot
{
    public class SafeList<T>
    {
        private readonly List<T> list;
        private readonly object _lock = new object();
        private static readonly string assemblyLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        private readonly string fileName;

        public SafeList(string name)
        {
            fileName = name + ".json";
            if (File.Exists(Path.Combine(assemblyLocation, fileName)))
                list = JsonConvert.DeserializeObject<List<T>>(File.ReadAllText(Path.Combine(assemblyLocation, fileName)));
            else
                list = new List<T>();
        }

        public void Add(T item)
        {
            lock(_lock)
            {
                list.Add(item);
                Write();
            }
        }

        public void Remove(T item)
        {
            lock(_lock)
            {
                list.Remove(item);
                Write();
            }
        }

        public void Clear()
        {
            lock(_lock)
            {
                list.Clear();
                Write();
            }
        }

        public List<T> ShallowCopy()
        {
            lock(_lock)
                return new List<T>(list);
        }

        public bool Contains(T item)
        {
            lock(_lock)
                return list.Contains(item);
        }

        public bool Any(Func<T, bool> predicate)
        {
            lock (_lock)
                return list.Any(predicate);
        }

        public T Find(Predicate<T> match)
        {
            lock (_lock)
                return list.Find(match);
        }

        public T FirstOrDefault(Func<T, bool> predicate)
        {
            lock (_lock)
                return list.FirstOrDefault(predicate);
        }

        private static Random rng = new Random();
        public void Shuffle()
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        private void Write()
        {
            File.WriteAllText(Path.Combine(assemblyLocation, fileName), JsonConvert.SerializeObject(list, Formatting.Indented));
        }
    }
}
