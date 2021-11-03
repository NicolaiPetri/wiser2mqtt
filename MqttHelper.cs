using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Wiser2Mqtt
{
    public class MqttHelper
    {
        private IMqttClient _mqttClient;
        private MqttOptions _options;
        public MqttHelper(IOptions<MqttOptions> mqttOptions)
        {
            _options = mqttOptions.Value;
            Initialize().Wait();
        }
        public async Task Initialize()
        {
            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(_options.Host, _options.Port) // Port is optional
            .Build();
            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();
            _mqttClient.UseConnectedHandler(async e =>
            {
                Console.WriteLine("### CONNECTED WITH SERVER ###");

                // Subscribe to a topic
                //   await mqttClient.SubscribeAsync(new MqttClientSubscribeOptionsBuilder().WithTopicFilter("#").Build());

                Console.WriteLine("### SUBSCRIBED ###");
            });
            await _mqttClient.ConnectAsync(options, CancellationToken.None);

            _mqttClient.UseApplicationMessageReceivedHandler(e =>
            {
                if (e.ApplicationMessage.Retain == false)
                {
                    Console.WriteLine("### RECEIVED APPLICATION MESSAGE ###");
                    Console.WriteLine($"+ Topic = {e.ApplicationMessage.Topic}");
                    Console.WriteLine($"+ Payload = {Encoding.UTF8.GetString(e.ApplicationMessage.Payload)}");
                    Console.WriteLine($"+ QoS = {e.ApplicationMessage.QualityOfServiceLevel}");
                    Console.WriteLine($"+ Retain = {e.ApplicationMessage.Retain}");
                    Console.WriteLine();
                }
            });
        }
        public async Task PublishMessage(string topic, string payload)
        {
            await _mqttClient.PublishAsync(topic, payload);

        }
    }
}