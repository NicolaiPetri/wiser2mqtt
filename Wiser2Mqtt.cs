using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Wiser2Mqtt
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }
        public static IConfiguration Configuration;
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    Configuration = hostContext.Configuration;
                    services.Configure<WiserOptions>(Configuration.GetSection(
                                                        WiserOptions.ConfigKey));
                    services.Configure<MqttOptions>(Configuration.GetSection(
                                                        MqttOptions.ConfigKey));
                    services.AddHttpClient<HttpClient>("wiser", a => new HttpClient());

                    services.AddSingleton<MqttHelper>();
                    services.AddHostedService<Worker>();
                });



        private static bool ValidateServerCertificate(HttpRequestMessage arg1, X509Certificate2 arg2, X509Chain arg3, SslPolicyErrors arg4)
        {
            return true;
        }

    }
}
