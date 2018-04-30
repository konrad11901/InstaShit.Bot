using System;
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
            Log.Write(text, LogType.Queue);
            base.Debug(text);
        }
        public string ErrorMessage { get; set; }
        public async Task<bool> Process()
        {
            try
            {
                if (!await TryLoginAsync())
                {
                    Debug("Can't log in.");
                    ErrorMessage = "An error occured while trying to log in.";
                    return false;
                }
                Debug("Successfully logged in!");
                while (true)
                {
                    var answer = await GetAnswerAsync();
                    if (answer == null)
                        break;
                    int sleepTime = SleepTime;
                    Debug($"Sleeping... ({sleepTime}ms)");
                    await Task.Delay(sleepTime);
                    Debug($"Atempting to answer (\"{answer.AnswerWord}\") question about word \"{answer.Word}\" with id {answer.WordId}");
                    if (!await TryAnswerQuestionAsync(answer))
                    {
                        Debug("Can't answer the question.");
                        ErrorMessage = $"An error occured while trying to answer the question about word \"{answer.Word}\" with id {answer.WordId}";
                        return false;
                    }
                    Debug("Success!");
                }
                SaveSessionData();
                return true;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return false;
            }
        }
    }
}
