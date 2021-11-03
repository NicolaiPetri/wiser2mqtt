using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wiser2Mqtt
{
    public class WiserOptions
    {
        public const string ConfigKey = "Wiser";
        public string Host { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public int UpdateInterval { get; set; }
    }
}
