using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using InstaShit.Bot.Models;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading;
using ConsoleTables;
using System.Security.Cryptography;

namespace InstaShit.Bot
{
    internal class Program
    {
        private static readonly string assemblyLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        /// <summary>
        /// Checks if the program can continue with its work.
        /// </summary>
        /// <returns>The boolean which specifies if the program can continue.</returns>
        public static bool CanContinue()
        {
            var input = Console.ReadLine();
            return input == "y";
        }
        static SafeList<User> users;
        static SafeList<string> usersToSkip;
        static Communication communication;
        static Settings settings;
        static SafeList<Tuple<string, DateTime, string>> whitelist;
        static async Task Main(string[] args)
        {
            Console.WriteLine("InstaShit.Bot - Telegram bot for Insta.Ling which automatically solves daily sessions");
            Console.WriteLine("Created by Konrad Krawiec \n");
            if (File.Exists(Path.Combine(assemblyLocation, "settings.json")))
                settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(Path.Combine(assemblyLocation, "settings.json")));
            else
                settings = GetSettingsFromUser();
            users = new SafeList<User>(nameof(users));
            usersToSkip = new SafeList<string>(nameof(usersToSkip));
            if (settings.Whitelist)
            {
                whitelist = new SafeList<Tuple<string, DateTime, string>>(nameof(whitelist));
                communication = new Communication(settings.TelegramBotToken, users, usersToSkip, whitelist);
            }
            else
                communication = new Communication(settings.TelegramBotToken, users, usersToSkip);
            communication.Start();
            InstaShitQueue queue = new InstaShitQueue(users, usersToSkip, communication);
            CancellationTokenSource source = new CancellationTokenSource();
            Task queueTask = queue.ProcessQueue(source.Token);
            Console.WriteLine("Successfully started.");
            while(true)
            {
                Console.Write("> ");
                string input = Console.ReadLine();
                if (queueTask.IsFaulted || queueTask.IsCanceled)
                {
                    try
                    {
                        await queueTask;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Unhandled Exception: " + ex);
                        break;
                    }
                }
                bool shouldBreak = false;
                switch(input.ToLower())
                {
                    case "/exit":
                        shouldBreak = true;
                        break;
                    case "/users add":
                        AddUser();
                        break;
                    case "/users remove":
                        RemoveUser();
                        break;
                    case "/broadcast":
                        Console.WriteLine("Enter message:");
                        var message = Console.ReadLine();
                        List<User> usersCopy = users.ShallowCopy();
                        foreach (User user in usersCopy)
                            await communication.SendMessageAsync(user.UserId, message);
                        break;
                    case "/check":
                        Console.WriteLine("OK");
                        break;
                    case "/queue refresh":
                        Console.WriteLine("Cancelling current queue task. Please be patient.");
                        source.Cancel();
                        try
                        {
                            await queueTask;
                        }
                        catch
                        {
                            File.Delete(Path.Combine(assemblyLocation, "queue.json"));
                            queue = new InstaShitQueue(users, usersToSkip, communication);
                            source = new CancellationTokenSource();
                            queueTask = queue.ProcessQueue(source.Token);
                            Console.WriteLine("Done!");
                        }
                        break;
                    case "/userstoskip add":
                        SkipUser();
                        break;
                    case "/userstoskip remove":
                        string login = GetStringFromUser("User login");
                        usersToSkip.Remove(login);
                        break;
                    case "/users":
                        ConsoleTable userTable = new ConsoleTable("Login", "User ID");
                        foreach (var user in users.ShallowCopy())
                            userTable.AddRow(user.Login, user.UserId);
                        userTable.Write(Format.MarkDown);
                        break;
                    case "/userstoskip":
                        ConsoleTable skipTable = new ConsoleTable("Login");
                        foreach (var user in usersToSkip.ShallowCopy())
                            skipTable.AddRow(user);
                        skipTable.Write(Format.MarkDown);
                        break;
                    case "/keys":
                        ConsoleTable table = new ConsoleTable("Key", "Creation date", "Notes");
                        foreach (var entry in whitelist.ShallowCopy())
                            table.AddRow(entry.Item1, entry.Item2, entry.Item3);
                        table.Write(Format.MarkDown);
                        break;
                    case "/keys generate":
                        int numberOfKeys = GetIntFromUser("Number of keys", 1, 1000);
                        string note = GetStringFromUser("Note (you can leave it empty)");
                        for (int i = 0; i < numberOfKeys; i++)
                        {
                            string key = GetUniqueKey();
                            whitelist.Add(new Tuple<string, DateTime, string>(key, DateTime.Now, note));
                            Console.WriteLine(key);
                        }
                        break;
                    case "/keys clear":
                        whitelist.Clear();
                        break;
                    case "/keys remove":
                        string keyToRemove = GetStringFromUser("Key to remove");
                        whitelist.Remove(whitelist.Find(t => t.Item1 == keyToRemove));
                        break;
                    case "/help":
                        Console.WriteLine("Usage:");
                        Console.WriteLine("/exit - Saves users queue and closes InstaShit.Bot");
                        Console.WriteLine("/broadcast - Broadcasts a message to all users");
                        Console.WriteLine("/queue refresh - Forces queue refresh");
                        Console.WriteLine("/users - Print all users");
                        Console.WriteLine("/users add - Adds user to the bot");
                        Console.WriteLine("/users remove - Removes user from the bot");
                        Console.WriteLine("/userstoskip - Print all users from the skip list");
                        Console.WriteLine("/userstoskip add - Add user to the skip list");
                        Console.WriteLine("/userstoskip remove - Remove user from the skip list");
                        if (settings.Whitelist)
                        {
                            Console.WriteLine("/keys - Lists all the whitelist keys");
                            Console.WriteLine("/keys generate - Generates one or more keys");
                            Console.WriteLine("/keys clear - Removes all the keys");
                            Console.WriteLine("/keys remove - Removes a specific key");
                        }
                        break;
                    default:
                        Console.WriteLine("Unknown command.");
                        break;
                }
                if (shouldBreak)
                    break;
            }
            Console.WriteLine("Starting cancellation process. It may take a long while to finish, please be patient.");
            source.Cancel();
            communication.Close();
            try
            {
                await queueTask;
            }
            catch
            {
                queue.SaveQueue();
                Console.WriteLine("Goodbye!");
            }
        }
        public static string GetUniqueKey()
        {
            int size = 16;
            byte[] data = new byte[size];
            RNGCryptoServiceProvider crypto = new RNGCryptoServiceProvider();
            crypto.GetBytes(data);
            return BitConverter.ToString(data).Replace("-", String.Empty);
        }
        private static Settings GetSettingsFromUser()
        {
            Console.WriteLine("Please enter the following data:");
            var settings = new Settings
            {
                TelegramBotToken = GetStringFromUser("Telegram bot API token")
            };
            Console.Write("Enable whitelisting (y/n)?");
            if (CanContinue())
                settings.Whitelist = true;
            Console.Write("Save these settings (y/n)? ");
            if (CanContinue())
                File.WriteAllText(Path.Combine(assemblyLocation, "settings.json"), JsonConvert.SerializeObject(settings, Formatting.Indented));
            return settings;
        }
        private static void SkipUser()
        {
            string login = GetStringFromUser("User login");
            usersToSkip.Add(login);
        }
        private static void RemoveUser()
        {
            try
            {
                string login = GetStringFromUser("User login");
                User userToRemove = users.Find(u => u.Login == login);
                users.Remove(userToRemove);
                Directory.Delete(Path.Combine(assemblyLocation, login), true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error!");
                Console.WriteLine(ex);
            }
        }
        private static void AddUser()
        {
            var settings = new InstaShitCore.Settings
            {
                Login = GetStringFromUser("Login"),
                Password = GetStringFromUser("Password"),
                MinimumSleepTime = GetIntFromUser("Minimum sleep time (in miliseconds)", 0, Int32.MaxValue),
                IntelligentMistakesData = new List<List<InstaShitCore.IntelligentMistakesDataEntry>>()
            };
            settings.MaximumSleepTime = GetIntFromUser("Maximum sleep time (in miliseconds)", settings.MinimumSleepTime, Int32.MaxValue);
            Console.Write("Specify IntelligentMistakesData for session number 1 (y/n)? ");
            if (CanContinue())
            {
                do
                {
                    var i = settings.IntelligentMistakesData.Count;
                    settings.IntelligentMistakesData.Add(new List<InstaShitCore.IntelligentMistakesDataEntry>());
                    do
                    {
                        Console.WriteLine($"IntelligentMistakeDataEntry number {settings.IntelligentMistakesData[i].Count + 1}");
                        var entry = new InstaShitCore.IntelligentMistakesDataEntry
                        {
                            RiskPercentage = GetIntFromUser("Risk of making the mistake (0-100)", 0, 100),
                            MaxNumberOfMistakes = GetIntFromUser("Maximum number of mistakes (-1 = unlimited)", -1, Int32.MaxValue)
                        };
                        settings.IntelligentMistakesData[i].Add(entry);
                        Console.Write("Add another entry (y/n)? ");
                    } while (CanContinue());
                    Console.Write($"Specify IntelligentMistakesData for session number {i + 2} (y/n)? ");
                } while (CanContinue());
                Console.Write("Allow typo in answer (y/n)? ");
                if (!CanContinue())
                    settings.AllowTypo = false;
                Console.Write("Allow synonyms (y/n)? ");
                if (!CanContinue())
                    settings.AllowSynonym = false;
            }
            User user = new User
            {
                Login = settings.Login,
                UserId = GetIntFromUser("User ID (-1 = dummy)", 0, int.MaxValue)
            };
            Directory.CreateDirectory(Path.Combine(assemblyLocation, user.Login));
            File.WriteAllText(Path.Combine(assemblyLocation, user.Login, "settings.json"), JsonConvert.SerializeObject(settings, Formatting.Indented));
            users.Add(user);
        }
        /// <summary>
        /// Gets the integer from user's input.
        /// </summary>
        /// <param name="valueName">The name of value to get.</param>
        /// <param name="minValue">Minimum accepted value.</param>
        /// <param name="maxValue">Maximum accepted value.</param>
        /// <returns>The integer.</returns>
        public static int GetIntFromUser(string valueName, int minValue, int maxValue)
        {
            while (true)
            {
                Console.Write($"{valueName}: ");
                if (int.TryParse(Console.ReadLine(), out var value) && value >= minValue && value <= maxValue)
                    return value;
                Console.WriteLine("Wrong input, try again.");
            }
        }
        /// <summary>
        /// Gets the string from user's input.
        /// </summary>
        /// <param name="valueName">The name of value to get.</param>
        /// <returns>The string.</returns>
        public static string GetStringFromUser(string valueName)
        {
            Console.Write($"{valueName}: ");
            return Console.ReadLine();
        }

    }
}
