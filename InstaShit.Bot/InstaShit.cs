using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InstaShit.Bot
{
    class InstaShit : InstaShitCore.InstaShitCore
    {
        public InstaShit(string baseLocation) : base(baseLocation)
        {

        }
        protected override void Debug(string text)
        {
            Log.Write(DateTime.UtcNow + ": " + text, LogType.Queue);
            base.Debug(text);
        }
        public async Task<bool> Process()
        {
            if (!await TryLoginAsync())
            {
                Log.Write(DateTime.UtcNow + ": Can't log in.", LogType.Queue);
                return false;
            }
            Log.Write(DateTime.UtcNow + ": Successfully logged in!", LogType.Queue);
            while (true)
            {
                var answer = await GetAnswerAsync();
                if (answer == null)
                    break;
                int sleepTime = SleepTime;
                Log.Write($"Sleeping... ({sleepTime}ms)", LogType.Queue);
                await Task.Delay(sleepTime);
                Log.Write($"Atempting to answer (\"{answer.AnswerWord}\") question about word \"{answer.Word}\" with id {answer.WordId}", LogType.Queue);
                if (!await TryAnswerQuestionAsync(answer))
                {
                    Log.Write("Can't answer the question.", LogType.Queue);
                    return false;
                }
                Log.Write("Success!", LogType.Queue);
            }
            SaveSessionData();
            return true;
        }
    }
}
