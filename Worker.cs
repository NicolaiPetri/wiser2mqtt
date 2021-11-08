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
        private readonly WiserOptions _options;
        public Worker(ILogger<Worker> logger, MqttHelper mqtt, IOptions<WiserOptions> wiserOptions)
        {
            _options = wiserOptions.Value;
            _logger = logger;
            _client = CreateCustomHttpClient();
            _client.BaseAddress = new Uri($"https://{_options.Host}");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(
            ASCIIEncoding.ASCII.GetBytes(
               $"{_options.Username}:{_options.Password}")));
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
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await FetchData();

                await Task.Delay(_options.UpdateInterval * 1000, stoppingToken);
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
                    respBody = CurlWrapper.DoCurlRequest($"https://{_options.Host}{url}", 
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
                    respBody = CurlWrapper.DoCurlRequest($"https://{_options.Host}{url}",
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
    }
}
