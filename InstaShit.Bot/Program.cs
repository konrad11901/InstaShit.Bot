using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using InstaShit.Bot.Models;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading;

namespace InstaShit.Bot
{
    public enum UserType { Telegram, Facebook };
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
        static async Task Main(string[] args)
        {
            Settings settings;
            if (File.Exists(Path.Combine(assemblyLocation, "settings.json")))
                settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(Path.Combine(assemblyLocation, "settings.json")));
            else
                settings = GetSettingsFromUser();
            Communication.Start(settings.TelegramBotToken);
            InstaShitQueue queue = new InstaShitQueue();
            CancellationTokenSource source = new CancellationTokenSource();
            Task queueTask = queue.ProcessQueue(source.Token);
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
                    case "/add":
                        AddUser();
                        break;
                    case "/remove":
                        RemoveUser();
                        break;
                    case "/broadcast":
                        Console.WriteLine("Enter message:");
                        var message = Console.ReadLine();
                        foreach (User user in Users.UsersList)
                            await Communication.SendMessageAsync(user.UserId, message);
                        break;
                    case "/check":
                        Console.WriteLine("OK");
                        break;
                    case "/queuerefresh":
                        Console.WriteLine("Cancelling current queue task. Please be patient.");
                        source.Cancel();
                        try
                        {
                            await queueTask;
                        }
                        catch
                        {
                            File.Delete(Path.Combine(assemblyLocation, "queue.json"));
                            queue = new InstaShitQueue();
                            source = new CancellationTokenSource();
                            queueTask = queue.ProcessQueue(source.Token);
                            Console.WriteLine("Done!");
                        }
                        break;
                    case "/skip":
                        SkipUser();
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
            Communication.Close();
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
        private static Settings GetSettingsFromUser()
        {
            Console.WriteLine("Please enter the following data:");
            var settings = new Settings
            {
                TelegramBotToken = GetStringFromUser("Telegram bot API token: ")
            };
            Console.Write("Save these settings (y/n)? ");
            if (CanContinue())
                File.WriteAllText(Path.Combine(assemblyLocation, "settings.json"), JsonConvert.SerializeObject(settings, Formatting.Indented));
            return settings;
        }
        private static void SkipUser()
        {
            string login = GetStringFromUser("User login");
            SkipUsers.Add(login);
        }
        private static void RemoveUser()
        {
            try
            {
                string login = GetStringFromUser("User login");
                User userToRemove = Users.UsersList.Find(u => u.Login == login);
                Users.Remove(userToRemove);
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
                UserType = UserType.Telegram,
                UserId = GetIntFromUser("User ID", 0, int.MaxValue)
            };
            Directory.CreateDirectory(Path.Combine(assemblyLocation, user.Login));
            File.WriteAllText(Path.Combine(assemblyLocation, user.Login, "settings.json"), JsonConvert.SerializeObject(settings, Formatting.Indented));
            Users.Add(user);
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
