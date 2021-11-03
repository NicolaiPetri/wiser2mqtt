using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace Wiser2Mqtt
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly HttpClient _client;
        private readonly MqttHelper _mqtt;
        private readonly Dictionary<string, JObject> _meterDetails = new();
        private readonly WiserOptions _options;
        public Worker(ILogger<Worker> logger, IHttpClientFactory clientFactory, MqttHelper mqtt, IOptions<WiserOptions> wiserOptions)
        {
            _options = wiserOptions.Value;
//            var (username, password) = ("m2madmin", "FmTyjSubjf8f");
            _logger = logger;
            _client = clientFactory.CreateClient("wiser"); ;
            _client.BaseAddress = new Uri($"https://{_options.Host}");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(
            System.Text.ASCIIEncoding.ASCII.GetBytes(
               $"{_options.Username}:{_options.Password}")));
            _mqtt = mqtt;
            DoInventory().Wait();
        }

        private async Task DoInventory()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "/rsa1/WirelessMeter/instances");
            var resp = await _client.SendAsync(req);
            var respBody = await resp.Content.ReadAsStringAsync();

            var respJson = JArray.Parse(respBody);
            foreach (JObject meter in respJson)
            {
                _meterDetails.Add(meter["slaveId"].ToString(), meter);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await FetchData();

                await Task.Delay(_options.UpdateInterval*1000, stoppingToken);
            }
        }

        private async Task FetchData()
        {
            var respInstantData = await FetchData("/rsa1/MeterInstantData");
            foreach (JObject mreading in respInstantData["MeterInstantData"] as JArray)
            {
                var meterId = mreading["slaveId"].ToString();
                var meter = _meterDetails[meterId];
                await _mqtt.PublishMessage($"wiser/{meter["zone"]}/MeterInstantData", mreading.ToString());
            }
            Console.WriteLine($"Got: {respInstantData}");
            var respCumulatedData = await FetchData("/rsa1/MeterCumulatedData");
            foreach (JObject mreading in respCumulatedData["MeterCumulatedData"] as JArray)
            {
                var meterId = mreading["slaveId"].ToString();
                var meter = _meterDetails[meterId];
                await _mqtt.PublishMessage($"wiser/{meter["zone"]}/MeterCumulatedData", mreading.ToString());
            }
            Console.WriteLine($"Got: {respCumulatedData}");
        }

        private async Task<JObject> FetchData(string url)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, url);
                var resp = await _client.SendAsync(req);
                var respBody = await resp.Content.ReadAsStringAsync();

                var respJson = JObject.Parse(respBody);
                return respJson;
            }
            catch (Exception e)
            {
                Console.WriteLine($"EX: {e}");
                throw;
            }
        }
    }
}
