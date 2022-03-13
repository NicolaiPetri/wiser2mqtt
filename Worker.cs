using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using wiser2mqtt;

namespace Wiser2Mqtt
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly HttpClient _client;
        private readonly MqttHelper _mqtt;
        private readonly Dictionary<string, JObject> _meterDetails = new();
        private readonly WiserOptions _wiserOptions;
        private readonly MqttOptions _mqttOptions;
        public Worker(ILogger<Worker> logger, MqttHelper mqtt, IOptions<WiserOptions> wiserOptions, IOptions<MqttOptions> mqttOptions)
        {
            _wiserOptions = wiserOptions.Value;
            _mqttOptions = mqttOptions.Value;
            _logger = logger;
            _client = CreateCustomHttpClient();
            _client.BaseAddress = new Uri($"https://{_wiserOptions.Host}");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(
            ASCIIEncoding.ASCII.GetBytes(
               $"{_wiserOptions.Username}:{_wiserOptions.Password}")));
            _mqtt = mqtt;
            DoInventory().Wait();
        }

        private HttpClient CreateCustomHttpClient()
        {
#if UNUSED
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var httpHandler = new SocketsHttpHandler();
            httpHandler.SslOptions = new SslClientAuthenticationOptions
            {
                ApplicationProtocols = new List<SslApplicationProtocol> {
                    SslApplicationProtocol.Http11
                },
                AllowRenegotiation = false,
                CertificateRevocationCheckMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck,
//                EncryptionPolicy = EncryptionPolicy.AllowNoEncryption,
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12,
                RemoteCertificateValidationCallback = delegate { return true; },
/*                CipherSuitesPolicy = new CipherSuitesPolicy(new[]
                {
                        TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA256,
                        TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256,
                        TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_CCM

                       
//                        TlsCipherSuite.ECDHE-ECDSA-AES128-GCM-SHA256
                })
*/
            };
#endif
            var http2Handler = new HttpClientHandler
            {
                CheckCertificateRevocationList = false,
                ClientCertificateOptions = ClientCertificateOption.Manual,
                SslProtocols = System.Security.Authentication.SslProtocols.Tls12,
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            var client = new HttpClient(http2Handler);

            //            var r = client.GetAsync("https://192.168.1.214/").Result;           

            return client;
        }

        private async Task DoInventory()
        {
            var respJson = await FetchDataArray("/rsa1/WirelessMeter/instances");
            foreach (JObject meter in respJson)
            {
                _meterDetails.Add(meter["slaveId"].ToString(), meter);
            }

            if (_mqttOptions.HassAutoDiscovery)
            {
                await SendAutoDiscoveryTopic();
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await FetchData();

                await Task.Delay(_wiserOptions.UpdateInterval * 1000, stoppingToken);
            }
        }

        private async Task FetchData()
        {
            var respInstantData = await FetchDataObj("/rsa1/MeterInstantData");
            foreach (JObject mreading in respInstantData["MeterInstantData"] as JArray)
            {
                var meterId = mreading["slaveId"].ToString();
                var meter = _meterDetails[meterId];
                await _mqtt.PublishMessage($"wiser/{meter["zone"]}/MeterInstantData", mreading.ToString());
            }
            Console.WriteLine($"Got: {respInstantData}");
            var respCumulatedData = await FetchDataObj("/rsa1/MeterCumulatedData");
            foreach (JObject mreading in respCumulatedData["MeterCumulatedData"] as JArray)
            {
                var meterId = mreading["slaveId"].ToString();
                var meter = _meterDetails[meterId];
                await _mqtt.PublishMessage($"wiser/{meter["zone"]}/MeterCumulatedData", mreading.ToString());
            }
            Console.WriteLine($"Got: {respCumulatedData}");
        }

        private async Task<JObject> FetchDataObj(string url)
        {
            try
            {
                string respBody;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    respBody = CurlWrapper.DoCurlRequest($"https://{_wiserOptions.Host}{url}", 
                        headers: new[] {
                            $"Authorization: {_client.DefaultRequestHeaders.Authorization}"
                        }
                    );
                }
                else
                {
                    var req = new HttpRequestMessage(HttpMethod.Get, url);
                    var resp = await _client.SendAsync(req);
                    respBody = await resp.Content.ReadAsStringAsync();
                }
                var respJson = JObject.Parse(respBody);
                return respJson;
            }
            catch (Exception e)
            {
                Console.WriteLine($"EX: {e}");
                throw;
            }
        }
        private async Task<JArray> FetchDataArray(string url)
        {
            try
            {
                string respBody;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    respBody = CurlWrapper.DoCurlRequest($"https://{_wiserOptions.Host}{url}",
                        headers: new[] {
                            $"Authorization: {_client.DefaultRequestHeaders.Authorization}"
                        }
                    );
                }
                else
                {
                    var req = new HttpRequestMessage(HttpMethod.Get, url);
                    var resp = await _client.SendAsync(req);
                    respBody = await resp.Content.ReadAsStringAsync();
                }
                var respJson = JArray.Parse(respBody);
                return respJson;
            }
            catch (Exception e)
            {
                Console.WriteLine($"EX: {e}");
                throw;
            }
        }

        private async Task SendAutoDiscoveryTopic()
        {
            foreach (var meter in _meterDetails.Values)
            {
                var device = new
                {
                    identifiers = new[] { meter["serialNumber"] },
                    manufacturer = "Schneider Electric",
                    model = meter["productType"],
                    name = meter["zone"],
                    sw_version = meter["swVersion"]
                };
                var payload = JsonConvert.SerializeObject(new
                {
                    name = $"{meter["zone"]} Accumulated Power",
                    state_topic = $"wiser/{meter["zone"]}/MeterCumulatedData",
                    device_class = "energy",
                    enabled_by_default = true,
                    json_attributes_topic = $"wiser/{meter["zone"]}/MeterCumulatedData",
                    state_class = "total_increasing",
                    unique_id = $"wiser_{meter["serialNumber"]}_accumulated",
                    unit_of_measurement = "kWh",
                    attributes = new
                    {
                        last_reset = "1970-01-01T00:00:00+00:00"
                    },
                    value_template = "{{ value_json.energyTActive | float / 1000 }}",
                    expire_after = _wiserOptions.UpdateInterval * 2,
                    device
                }, Formatting.Indented);
                await _mqtt.PublishMessage($"homeassistant/sensor/{meter["serialNumber"]}/accumulated/config", payload, true);

                // TODO: I only have the 3-phase device so far, so I'm not 100% sure if this works for the smaller ones. Needs confirmation
                var phasePrefix = "PhaseSeq";
                // Example value of phSequence: PhaseSeqABC - I'm guessing this indicates there will be measurements for phase A, B and C.
                var phases = meter["phSequence"].ToString().Substring(phasePrefix.Length);
                foreach (var phase in phases)
                {
                    payload = JsonConvert.SerializeObject(new
                    {
                        name = $"{meter["zone"]} Power {phase}",
                        state_topic = $"wiser/{meter["zone"]}/MeterInstantData",
                        device_class = "power",
                        enabled_by_default = true,
                        json_attributes_topic = $"wiser/{meter["zone"]}/MeterInstantData",
                        state_class = "measurement",
                        unique_id = $"wiser_{meter["serialNumber"]}_power_{phase}",
                        unit_of_measurement = "W",
                        value_template = "{{ value_json.power" + phase + " }}",
                        expire_after = _wiserOptions.UpdateInterval * 2,
                        device
                    }, Formatting.Indented);
                    await _mqtt.PublishMessage($"homeassistant/sensor/{meter["serialNumber"]}/instant-{phase}/config", payload, true);
                }
                
            }
        }
    }
}
