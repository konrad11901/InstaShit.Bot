using System;
using System.Collections.Generic;
using System.Text;

namespace InstaShit.Bot.Models
{
    public class User
    {
        public string Login { get; set; }
        public UserType UserType { get; set; }
        public int UserId { get; set; }
    }
}
