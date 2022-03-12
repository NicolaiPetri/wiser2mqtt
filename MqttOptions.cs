using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wiser2Mqtt
{
    public class MqttOptions
    {
        public const string ConfigKey = "Mqtt";
        public string Host { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool HassAutoDiscovery { get; set; }
    }
}
