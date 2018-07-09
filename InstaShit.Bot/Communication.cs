using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;
using Newtonsoft.Json;
using InstaShit.Bot.Models;
using System.IO;
using System.Reflection;
using System.Threading;

namespace InstaShit.Bot
{
    public class Communication
    {
        public TelegramBotClient Bot;
        private Dictionary<int, int> userStep = new Dictionary<int, int>();
        private readonly string token;
        private readonly string assemblyLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        readonly SafeList<User> users;
        readonly SafeList<string> usersToSkip;
        readonly SafeList<Tuple<string, DateTime, string>> whitelist;
        readonly bool useWhitelist = false;
        public Communication(string token, SafeList<User> users, SafeList<string> usersToSkip,
            SafeList<Tuple<string, DateTime, string>> whitelist) : this(token, users, usersToSkip)
        {
            useWhitelist = true;
            this.whitelist = whitelist;
        }
        public Communication(string token, SafeList<User> users, SafeList<string> usersToSkip)
        {
            this.token = token;
            this.users = users;
            this.usersToSkip = usersToSkip;
            Bot = new TelegramBotClient(token);
            Bot.OnMessage += BotOnMessageReceived;
        }
        public void Start()
        {
            Bot.StartReceiving();
        }
        public void Close()
        {
            Bot.StopReceiving();
        }
        // This method is an attempt to solve the request timed out issue
        public async Task SendMessageAsync(long userId, string message)
        {
            Log.Write($"Trying to send message \"{message}\" to user {userId.ToString()}", LogType.Communication);
            if (userId == -1)
            {
                Log.Write("Skipping message because user is dummy", LogType.Communication);
                return;
            }
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    await Bot.SendTextMessageAsync(userId, message, cancellationToken: new CancellationTokenSource(5000 + (10000 * i)).Token);
                    Log.Write("Message sent!", LogType.Communication);
                    break;
                }
                catch (Exception ex)
                {
                    Log.Write($"An error occured, retrying ({ex})", LogType.Communication);
                    if (i == 4)
                    {
                        Log.Write("[CRITICAL] Message can't be sent, please check Telegram status!", LogType.Communication);
                    }
                }
            }
        }
        private async Task SendFileAsync(long userId, string filePath)
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    await Bot.SendDocumentAsync(userId, 
                        new Telegram.Bot.Types.InputFiles.InputOnlineFile(new FileStream(filePath, FileMode.Open, FileAccess.Read), Path.GetFileName(filePath)), 
                        cancellationToken: new CancellationTokenSource(10000 + (10000 * i)).Token);
                    break;
                }
                catch
                {
                    if (i == 4)
                        throw;
                }
            }
        }
        private async void BotOnMessageReceived(object sender, MessageEventArgs args)
        {
            Telegram.Bot.Types.Message message = args.Message;
            if(message.Type == MessageType.Document && userStep.ContainsKey(message.From.Id) && userStep[message.From.Id] == 1)
            {
                Telegram.Bot.Types.File file = await Bot.GetFileAsync(message.Document.FileId);
                try
                {
                    string content;
                    using (var client = new WebClient())
                        content = await client.DownloadStringTaskAsync($"https://api.telegram.org/file/bot{token}/{file.FilePath}");
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
                    InstaShit instaShit = new InstaShit(settings);
                    if(!await instaShit.TryLoginAsync())
                    {
                        await SendMessageAsync(message.Chat.Id, "Incorrect login or password.");
                        return;
                    }
                    Directory.CreateDirectory(Path.Combine(assemblyLocation, settings.Login));
                    File.WriteAllText(Path.Combine(assemblyLocation, settings.Login, "settings.json"), JsonConvert.SerializeObject(settings, Formatting.Indented));
                    userStep.Remove(message.From.Id);
                    User user = new User()
                    {
                        Login = settings.Login,
                        UserId = message.From.Id
                    };
                    users.Add(user);
                    await SendMessageAsync(message.Chat.Id, "User successfully added!\n" +
                        "You'll be added to queue at the next queue refresh (9:00 Polish time every day).");
                }
                catch
                {
                    await SendMessageAsync(message.Chat.Id, "An error occured while trying to get settings. " +
                        "Make sure you're sending the right file.");
                }
            }
            if(message.Type == MessageType.Text)
            {
                Log.Write($"Received message from user {message.From.Id}: {message.Text}", LogType.Communication);
                switch(message.Text.ToLower())
                {
                    case "/start":
                        await SendMessageAsync(message.Chat.Id, "Hi! I'm InstaShit, a bot " +
                            "for Insta.Ling which automatically solves daily sessions.\nTo get started, " +
                            " type /configure. For more information, type /help.");
                        break;
                    case "/configure":
                        if(users.Any(u => u.UserId == message.From.Id))
                        {
                            await SendMessageAsync(message.Chat.Id, "Already configured.");
                            return;
                        }
                        if(!userStep.ContainsKey(message.From.Id))
                        {
                            if(useWhitelist)
                            {
                                userStep.Add(message.From.Id, 4);
                                await SendMessageAsync(message.Chat.Id, "This instance of InstaShit.Bot has whitelisting " +
                                    "enabled. Please enter the unique key received from the server administrator. " +
                                    "If you don't have it, contact server's owner or use another instance of the " +
                                    "bot which is publically available.\nTo abort, type /cancel.");
                            }
                            else
                            {
                                userStep.Add(message.From.Id, 1);
                                await SendMessageAsync(message.Chat.Id, "Please attach the InstaShit settings file.\n" +
                                                       "You can use InstaShit.CLI to generate it. You can also " +
                                                       "share settings directly from InstaShit.Android " +
                                                       "(since version 0.5) - just go to \"Edit settings\", \"Advanced mode\" " +
                                                       "and touch \"Share\" button. Ability to create new settings directly " +
                                                       "from bot coming soon!\nType /cancel to abort this action.");
                            }
                        }
                        break;
                    case "/dictionary":
                        var user = users.FirstOrDefault(u => u.UserId == message.From.Id);
                        if (user == null)
                        {
                            await SendMessageAsync(message.Chat.Id, "No configuration found.");
                            return;
                        }
                        if (File.Exists(Path.Combine(assemblyLocation, user.Login, "wordsDictionary.json")))
                            await SendFileAsync(message.Chat.Id, Path.Combine(assemblyLocation, user.Login, "wordsDictionary.json"));
                        else
                            await SendMessageAsync(message.Chat.Id, "Dictionary file doesn't exist.");
                        break;
                            
                    case "/cancel":
                        if (userStep.ContainsKey(message.From.Id))
                        {
                            userStep.Remove(message.From.Id);
                            await SendMessageAsync(message.Chat.Id, "Cancelled!");
                        }
                        break;
                    case "/remove":
                        if(!users.Any(u => u.UserId == message.From.Id))
                        {
                            await SendMessageAsync(message.Chat.Id, "No configuration found.");
                            return;
                        }
                        if(!userStep.ContainsKey(message.From.Id))
                        {
                            userStep.Add(message.From.Id, 2);
                            await SendMessageAsync(message.Chat.Id, "You can stop automatic InstaShit session " +
                                "solving if you wish. Please note that this action won't cancel ongoing InstaShit session, " +
                                "if there's any.\nIf you want to continue, type /remove again. " +
                                "To cancel, type /cancel.");
                        }
                        else if (userStep[message.From.Id] == 2)
                        {
                            userStep.Remove(message.From.Id);
                            User userToRemove = users.Find(u => u.UserId == message.From.Id);
                            users.Remove(userToRemove);
                            try
                            {
                                Directory.Delete(Path.Combine(assemblyLocation, userToRemove.Login), true);
                                await SendMessageAsync(message.Chat.Id, "Successfully removed.");
                            }
                            catch
                            {
                                await Task.Delay(3000);
                                try
                                {
                                    Directory.Delete(Path.Combine(assemblyLocation, userToRemove.Login), true);
                                    await SendMessageAsync(message.Chat.Id, "Successfully removed.");
                                }
                                catch(Exception ex)
                                {
                                    Log.Write("ERROR: " + ex, LogType.Communication);
                                    await SendMessageAsync(message.Chat.Id, "An error occured while trying to remove " +
                                        "your files. The administrator has been notifies about this issue.");
                                }
                            }
                        }
                        break;
                    case "/skip":
                        if (!users.Any(u => u.UserId == message.From.Id))
                        {
                            await SendMessageAsync(message.Chat.Id, "No configuration found.");
                            return;
                        }
                        if (usersToSkip.Contains(users.Find(u => u.UserId == message.From.Id).Login))
                        {
                            await SendMessageAsync(message.Chat.Id, "Already on the skip list.");
                            return;
                        }
                        if (!userStep.ContainsKey(message.From.Id))
                        {
                            userStep.Add(message.From.Id, 3);
                            await SendMessageAsync(message.Chat.Id, "You can skip the next InstaShit session if you wish. " +
                                " Please note that this action won't cancel ongoing InstaShit session, if there's any." +
                                "\nIf you want to continue, type /skip again. To cancel, type /cancel.");
                        }
                        else if (userStep[message.From.Id] == 3)
                        {
                            userStep.Remove(message.From.Id);
                            usersToSkip.Add(users.Find(u => u.UserId == message.From.Id).Login);
                            await SendMessageAsync(message.Chat.Id, "Successfully added to skip list.");
                        }
                        break;
                    default:
                        if (userStep.ContainsKey(message.From.Id) && userStep[message.From.Id] == 4)
                        {
                            if(whitelist.Any(t => t.Item1 == message.Text))
                            {
                                whitelist.Remove(whitelist.Find(t => t.Item1 == message.Text));
                                userStep[message.From.Id] = 1;
                                await SendMessageAsync(message.Chat.Id, "Success!");
                                await SendMessageAsync(message.Chat.Id, "Please attach the InstaShit settings file.\n" +
                                    "You can use InstaShit.CLI to generate it. You can also share settings directly " +
                                    "from InstaShit.Android (since version 0.5) - just go to \"Edit settings\", " +
                                    "\"Advanced mode\" and touch \"Share\" button. Ability to create new settings " +
                                    "using the bot coming soon!\nType /cancel to abort this action (you'll need " +
                                    "a new unique key if you try to configure the bot again in the future).");
                            }
                            else
                            {
                                await SendMessageAsync(message.Chat.Id, "Incorrect key.");
                            }
                            break;  
                        }
                        await SendMessageAsync(message.Chat.Id, "Usage:\n" +
                            "/configure - Configures InstaShit bot\n" +
                            "/skip - Skips next InstaShit session\n" +
                            "/remove - Unregisters from the bot\n" +
                            "/cancel - Cancels any ongoing process (configure/remove/skip)\n" +
                            "/dictionary - Returns the wordsDictionary.json file");
                        break;
                }
            }
        }
    }
}
