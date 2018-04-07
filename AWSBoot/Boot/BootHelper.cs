using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.EC2;
using Amazon.Extensions.NETCore.Setup;
using Amazon.SimpleSystemsManagement;
using Microsoft.Extensions.DependencyInjection;

namespace AWSBoot.Boot
{
    public class BootHelper
    {
        private const string DefaultEnvironment = "LocalDevelopment";

        public async static Task<BootConfig> GetConfig()
        {
            if(!AWSEnvironmentService.IsEC2Instance())
            {
                // Doesn't look like we're running on EC2. We'll assume we're running locally
                return GetDefaultConfig();
            }
            
            var services = GetServices();

            var env = services.GetService<AWSEnvironmentService>();

            var boot = new BootConfig();

            boot.Environment = await env.GetTagValue("environment");

            if (String.IsNullOrWhiteSpace(boot.Environment))
                throw new Exception("No 'environment' tag found on the EC2 instance.");

            boot.LoggingEnabled = !(await env.HasTag("logging", "off"));

            // GET THE CONFIG PARAMETERS FROM AWS PARAMETER STORE
            var prefix = $"/{boot.Environment.ToLower()}/";
            var dict = await env.GetParameters(prefix);

            // Strip the prefix so that they play well with ASPNETCORE config
            boot.Parameters = dict.ToDictionary(d => d.Key.Substring(prefix.Length).Replace("/", ":"), d => d.Value);
 
            return boot;
        }

        private static ServiceProvider GetServices()
        {
            // Setup DI
            return new ServiceCollection()
                        .AddDefaultAWSOptions(new AWSOptions())
                        .AddAWSService<IAmazonEC2>()
                        .AddAWSService<IAmazonSimpleSystemsManagement>()
                        .AddScoped<AWSEnvironmentService>()
                        .BuildServiceProvider();
        }

        private static BootConfig GetDefaultConfig()
        {
            return new BootConfig() {
                Parameters = new Dictionary<string, string>(),
                Environment = DefaultEnvironment
            };
        }
    }

    public class BootConfig
    {
        public string Environment { get; set; }

        public IDictionary<string, string> Parameters { get; set; }

        public bool LoggingEnabled { get; set; }
    }
}