﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;
using Newtonsoft.Json;
using InstaShit.Bot.Models;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Threading;

namespace InstaShit.Bot
{
    public static class Communication
    {
        public static TelegramBotClient Bot;
        private static Dictionary<int, int> userStep = new Dictionary<int, int>();
        private static string _token;
        private static readonly string assemblyLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        public static void Start(string token)
        {
            Bot = new TelegramBotClient(token);
            _token = token;
            Bot.OnMessage += BotOnMessageReceived;
            Bot.StartReceiving();
        }
        public static void Close()
        {
            Bot.StopReceiving();
        }
        // This method is an attempt to solve the request timed out issue
        public static async Task SendMessageAsync(long userId, string message)
        {
            for(int i = 0; i < 5; i++)
            {
                try
                {
                    await Bot.SendTextMessageAsync(userId, message, cancellationToken: new CancellationTokenSource(5000 + (10000 * i)).Token);
                    break;
                }
                catch
                {
                    if (i == 4)
                        throw;
                }
            }
        }
        private static async void BotOnMessageReceived(object sender, MessageEventArgs args)
        {
            Telegram.Bot.Types.Message message = args.Message;
            if(message.Type == MessageType.Document && userStep.ContainsKey(message.From.Id) && userStep[message.From.Id] == 1)
            {
                Telegram.Bot.Types.File file = await Bot.GetFileAsync(message.Document.FileId);
                try
                {
                    string content;
                    using (var client = new WebClient())
                        content = await client.DownloadStringTaskAsync($"https://api.telegram.org/file/bot{_token}/{file.FilePath}");
                    InstaShitCore.Settings settings = JsonConvert.DeserializeObject<InstaShitCore.Settings>(content);
                    if(Directory.Exists(Path.Combine(assemblyLocation, settings.Login)))
                    {
                        await SendMessageAsync(message.From.Id, "This user is already registered. " +
                            "If you have recently unregisted from this bot, you have to wait up to 24 hours " +
                            "before registering again.");
                        return;
                    }
                    if (settings.MinimumSleepTime < 3000 || settings.MinimumSleepTime > 10000)
                    {
                        await SendMessageAsync(message.Chat.Id, "MinimumSleepTime must be at least " +
                            "3000 but no more than 10000.");
                        return;
                    }
                    if (settings.MaximumSleepTime < 5000 || settings.MaximumSleepTime > 15000)
                    {
                        await SendMessageAsync(message.Chat.Id, "MaximumSleepTime must be at least " +
                            "5000 but no more than 15000.");
                        return;
                    }
                    if (settings.MaximumSleepTime - settings.MinimumSleepTime < 2000)
                    {
                        await SendMessageAsync(message.Chat.Id, "The difference between " +
                            "MaximumSleepTime and MinimumSleepTime must be at least 2000 and " +
                            "MaximumSleepTime must be greater than MinimumSleepTime.");
                        return;
                    }
                    Directory.CreateDirectory(Path.Combine(assemblyLocation, settings.Login));
                    File.WriteAllText(Path.Combine(assemblyLocation, settings.Login, "settings.json"), JsonConvert.SerializeObject(settings, Formatting.Indented));
                    InstaShit instaShit = new InstaShit(Path.Combine(assemblyLocation, settings.Login));
                    if(!await instaShit.TryLoginAsync())
                    {
                        Directory.Delete(Path.Combine(assemblyLocation, settings.Login), true);
                        await SendMessageAsync(message.Chat.Id, "Incorrect login or password.");
                        return;
                    }
                    userStep.Remove(message.From.Id);
                    User user = new User()
                    {
                        Login = settings.Login,
                        UserId = message.From.Id,
                        UserType = UserType.Telegram
                    };
                    Users.Add(user);
                    await SendMessageAsync(message.Chat.Id, "User successfully added!\n" +
                        "You'll be added to queue at the next queue refresh (8:00 UTC every day).");
                }
                catch
                {
                    await SendMessageAsync(message.Chat.Id, "An error occured while trying to get settings. " +
                        "Make sure you're sending the right file.");
                }
            }
            if(message.Type == MessageType.Text)
            {
                switch(message.Text.ToLower())
                {
                    case "/start":
                        await SendMessageAsync(message.Chat.Id, "Hi! I'm InstaShit, a bot " +
                            "for Insta.Ling which automatically solves daily sessions.\nTo get started, " +
                            " type /configure. For more information, type /help.");
                        break;
                    case "/configure":
                        if(Users.UsersList.Any(u => u.UserId == message.From.Id))
                        {
                            await SendMessageAsync(message.Chat.Id, "Already configured.");
                            return;
                        }
                        if(!userStep.ContainsKey(message.From.Id))
                        {
                            userStep.Add(message.From.Id, 1);
                            await SendMessageAsync(message.Chat.Id, "Please attach the InstaShit settings file. " +
                                "You can use InstaShit.CLI to generate it. Ability to create new settings directly from " +
                                "bot coming soon!\nType /cancel to abort this action.");
                        }
                        break;
                    case "/cancel":
                        if (userStep.ContainsKey(message.From.Id))
                        {
                            userStep.Remove(message.From.Id);
                            await SendMessageAsync(message.Chat.Id, "Cancelled!");
                        }
                        break;
                    case "/remove":
                        if(!Users.UsersList.Any(u => u.UserId == message.From.Id))
                        {
                            await SendMessageAsync(message.Chat.Id, "No configuration found.");
                            return;
                        }
                        if(!userStep.ContainsKey(message.From.Id))
                        {
                            userStep.Add(message.From.Id, 2);
                            await SendMessageAsync(message.Chat.Id, "You can stop automatic InstaShit session " +
                                "solving if you wish. Please note that this action won't cancel ongoing InstaShit session, " +
                                "if there's any in progress. Your InstaShit settings file and other data will be removed " +
                                "within 24 hours.\nIf you want to continue, type /remove again. To cancel, type /cancel.");
                        }
                        else if (userStep[message.From.Id] == 2)
                        {
                            userStep.Remove(message.From.Id);
                            User userToRemove = Users.UsersList.Find(u => u.UserId == message.From.Id);
                            Users.Remove(userToRemove);
                            await SendMessageAsync(message.Chat.Id, "Successfully removed.");
                        }
                        break;
                    default:
                        await SendMessageAsync(message.Chat.Id, "Usage:\n" +
                            "/configure - Configures InstaShit bot\n" +
                            "/remove - Unregisters from the bot\n" +
                            "/cancel - Cancels any ongoing process (configure/remove)");
                        break;
                }
            }
        }
    }
}