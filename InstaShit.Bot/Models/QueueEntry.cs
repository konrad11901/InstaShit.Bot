using System;
using System.Collections.Generic;
using System.Text;

namespace InstaShit.Bot.Models
{
    public class QueueEntry
    {
        public DateTime ProcessTime { get; set; }
        public User User { get; set; }
    }
}
