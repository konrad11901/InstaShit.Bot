using System;
using System.Collections.Generic;
using System.IO;
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
        public List<string> Log { get; private set; } = new List<string>();
        public InstaShitQueue()
        {
            if (File.Exists(Path.Combine(assemblyLocation, "queue.json")))
            {
                usersQueue = JsonConvert.DeserializeObject<Queue<QueueEntry>>(File.ReadAllText(Path.Combine(assemblyLocation, "queue.json")));
                if (DateTime.UtcNow.Hour >= 8)
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
                if (DateTime.UtcNow >= DateTime.UtcNow.Date.AddHours(8) && dateTime != DateTime.UtcNow.Date)
                {
                    Log.Add(DateTime.UtcNow + ": Refreshing queue...");
                    List<User> users = Users.UsersList;
                    users.Shuffle();
                    DateTime tmpDate = DateTime.UtcNow;
                    foreach (User user in users)
                    {
                        tmpDate = tmpDate.AddMinutes(rnd.Next(20, 41)).AddSeconds(rnd.Next(0, 60));
                        usersQueue.Enqueue(new QueueEntry() { User = user, ProcessTime = tmpDate });
                        Log.Add(DateTime.UtcNow + $": Queued user {user.Login} at {tmpDate}");
                        await Communication.SendMessageAsync(user.UserId, "Your InstaShit session " +
                            $"will be started at {tmpDate} UTC (with max. 1 minute delay). Please don't attempt " +
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
                        if (!Users.UsersList.Contains(entry.User))
                        {
                            Directory.Delete(Path.Combine(assemblyLocation, entry.User.Login), true);
                            continue;
                        }
                        InstaShit instaShit = new InstaShit(Path.Combine(assemblyLocation, entry.User.Login));
                        Log.Add(DateTime.UtcNow + $": Starting InstaShit for user {entry.User.Login}");
                        await Communication.SendMessageAsync(entry.User.UserId, "Starting session.");
                        cancellationToken.ThrowIfCancellationRequested();
                        await instaShit.Process();
                        Log.Add(DateTime.UtcNow + $": Session finished");
                        await Communication.SendMessageAsync(entry.User.UserId, "Session successfully " +
                            "finished.");
                        var childResults = await instaShit.GetResultsAsync();
                        StringBuilder messageToSend = new StringBuilder();
                        if(childResults.PreviousMark != "NONE")
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
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    if(usersQueue.Count > 0)
                    {
                        TimeSpan span = usersQueue.Peek().ProcessTime - DateTime.UtcNow;
                        Log.Add("Waiting " + (int)span.TotalMilliseconds + " milliseconds...");
                        await Task.Delay((int)span.TotalMilliseconds, cancellationToken);
                    }
                }
                TimeSpan span2;
                if (DateTime.UtcNow.Hour < 8)
                    span2 = DateTime.UtcNow.Date.AddHours(8) - DateTime.UtcNow;
                else
                    span2 = DateTime.UtcNow.Date.AddDays(1).AddHours(8) - DateTime.UtcNow;
                Log.Add("Waiting " + (int)span2.TotalMilliseconds + " milliseconds...");
                await Task.Delay((int)span2.TotalMilliseconds, cancellationToken);
            }
        }
    }
}
