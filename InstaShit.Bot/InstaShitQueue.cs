using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InstaShit.Bot.Models;
using Newtonsoft.Json;

namespace InstaShit.Bot
{
    public class InstaShitQueue
    {
        private string assemblyLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        private Queue<QueueEntry> usersQueue;
        private DateTime dateTime = DateTime.UtcNow.Date.AddDays(-1);
        private Random rnd = new Random();
        private readonly object _lock = new object();
        public InstaShitQueue()
        {
            if (File.Exists(Path.Combine(assemblyLocation, "queue.json")))
            {
                usersQueue = JsonConvert.DeserializeObject<Queue<QueueEntry>>(File.ReadAllText(Path.Combine(assemblyLocation, "queue.json")));
                if (DateTime.UtcNow.Hour >= 7)
                    dateTime = DateTime.UtcNow.Date;
            }
            else
                usersQueue = new Queue<QueueEntry>();
        }
        public void SaveQueue()
        {
            File.WriteAllText(Path.Combine(assemblyLocation, "queue.json"), JsonConvert.SerializeObject(usersQueue));
        }
        public async Task ProcessQueue(CancellationToken cancellationToken)
        {
            while(true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (DateTime.UtcNow >= DateTime.UtcNow.Date.AddHours(7) && dateTime != DateTime.UtcNow.Date)
                {
                    Log.Write("Refreshing queue...", LogType.Queue);
                    List<User> users = Users.UsersList;
                    users.Shuffle();
                    DateTime tmpDate = DateTime.UtcNow;
                    foreach (User user in users)
                    {
                        if (SkipUsers.SkipUsersList.Contains(user.Login))
                        {
                            Log.Write($"Skipping user {user.Login}", LogType.Queue);
                            SkipUsers.Remove(user.Login);
                            continue;
                        }
                        tmpDate = tmpDate.AddMinutes(rnd.Next(20, 41)).AddSeconds(rnd.Next(0, 60));
                        usersQueue.Enqueue(new QueueEntry() { User = user, ProcessTime = tmpDate });
                        Log.Write($"Queued user {user.Login} at {tmpDate}", LogType.Queue);
                        await Communication.SendMessageAsync(user.UserId, "Your InstaShit session " +
                            $"will be started at {tmpDate.AddHours(2).ToString("HH:mm:ss")} Polish time (with max. 1 minute delay). Please don't attempt " +
                            "to start Insta.Ling session at this time, even from other InstaShit apps. " +
                            "You'll be notified when your session finishes.");
                    }
                    dateTime = DateTime.UtcNow.Date;
                }
                while (usersQueue.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    QueueEntry entry = usersQueue.Peek();
                    if(DateTime.UtcNow >= entry.ProcessTime)
                    {
                        usersQueue.Dequeue();
                        if (!Users.UsersList.Any(u => u.Login == entry.User.Login))
                        {
                            Directory.Delete(Path.Combine(assemblyLocation, entry.User.Login), true);
                            continue;
                        }
                        if (SkipUsers.SkipUsersList.Contains(entry.User.Login))
                        {
                            Log.Write($"Skipping user {entry.User.Login}", LogType.Queue);
                            SkipUsers.Remove(entry.User.Login);
                            continue;
                        }
                        InstaShit instaShit;
                        try
                        {
                            instaShit = new InstaShit(Path.Combine(assemblyLocation, entry.User.Login));
                        }
                        catch(Exception ex)
                        {
                            Log.Write("Can't create InstaShit object: " + ex, LogType.Queue);
                            await Communication.SendMessageAsync(entry.User.UserId, "An internal error occured. " +
                                "It has been reported to the administrator.");
                            continue;
                        }
                        Log.Write($"Starting InstaShit for user {entry.User.Login}", LogType.Queue);
                        await Communication.SendMessageAsync(entry.User.UserId, "Starting session.");
                        cancellationToken.ThrowIfCancellationRequested();
                        if(!await instaShit.Process())
                        {
                            await Communication.SendMessageAsync(entry.User.UserId, "An error occured " +
                                "while processing your session. Please try to solve session using " +
                                "InstaShit.CLI or InstaShit.Android. Please also check if Insta.Ling website " +
                                "is down. If you think it's InstaShit's fault, create an issue on GitHub: " +
                                "https://github.com/konrad11901/InstaShit.Bot if it applies only to InstaShit.Bot or " +
                                "https://github.com/konrad11901/InstaShit if it affects all InstaShit apps.");
                            await Communication.SendMessageAsync(entry.User.UserId, $"Detailed information: {instaShit.ErrorMessage}");
                            Log.Write("Can't solve session, moving to next person", LogType.Queue);
                        }
                        else
                        {
                            Log.Write($" Session finished", LogType.Queue);
                            await Communication.SendMessageAsync(entry.User.UserId, "Session successfully " +
                                "finished.");
                            var childResults = await instaShit.GetResultsAsync();
                            StringBuilder messageToSend = new StringBuilder();
                            if (childResults.PreviousMark != "NONE")
                            {
                                messageToSend.AppendLine($"Mark from previous week: {childResults.PreviousMark}");
                            }
                            messageToSend.AppendLine($"Days of work in this week: {childResults.DaysOfWork}");
                            messageToSend.AppendLine($"From extracurricular words: +{childResults.ExtraParentWords}");
                            messageToSend.AppendLine($"Teacher's words: {childResults.TeacherWords}");
                            messageToSend.AppendLine($"Extracurricular words in current edition: {childResults.ParentWords}");
                            messageToSend.AppendLine($"Mark as of today at least: {childResults.CurrrentMark}");
                            messageToSend.Append($"Days until the end of this week: {childResults.WeekRemainingDays}");
                            await Communication.SendMessageAsync(entry.User.UserId, messageToSend.ToString());
                        }
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    if(usersQueue.Count > 0)
                    {
                        TimeSpan span = usersQueue.Peek().ProcessTime - DateTime.UtcNow;
                        if(span.TotalMilliseconds > 0)
                        {
                            Log.Write("Waiting " + (int)span.TotalMilliseconds + " milliseconds for next person...", LogType.Queue);
                            await Task.Delay((int)span.TotalMilliseconds, cancellationToken);
                        }
                    }
                }
                TimeSpan span2;
                if (DateTime.UtcNow.Hour < 7)
                    span2 = DateTime.UtcNow.Date.AddHours(7) - DateTime.UtcNow;
                else
                    span2 = DateTime.UtcNow.Date.AddDays(1).AddHours(7) - DateTime.UtcNow;
                Log.Write("Waiting " + (int)span2.TotalMilliseconds + " milliseconds for next queue refresh...", LogType.Queue);
                await Task.Delay((int)span2.TotalMilliseconds, cancellationToken);
            }
        }
    }
}
