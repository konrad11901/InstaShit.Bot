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
        public List<string> Log { get; set; } = new List<string>();
        protected override void Debug(string text)
        {
            Log.Add(DateTime.UtcNow + ": " + text);
            base.Debug(text);
        }
        public async Task Process()
        {
            if (!await TryLoginAsync())
            {

            }
            while(true)
            {
                var answer = await GetAnswerAsync();
                if (answer == null)
                    break;
                await Task.Delay(SleepTime);
                await TryAnswerQuestionAsync(answer);
            }
            SaveSessionData();
        }
    }
}
