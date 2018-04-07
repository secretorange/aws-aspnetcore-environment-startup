using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AWSBoot.Boot;

namespace AWSBoot
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost().Run();
        }


        public static IWebHost BuildWebHost()
        {
            // ===================================
            // Get the boot config from the server
            // ===================================
            var bootConfig = Task.Run(() => BootHelper.GetConfig()).Result;

            var webHost = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .ConfigureAppConfiguration((context, config) =>
                {
                    // !!! IMPORTANT !!!
                    // Set the environment from boot config
                    context.HostingEnvironment.EnvironmentName = bootConfig.Environment;

                    config.AddJsonFile("appsettings.json", optional: true)
                          .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);

                    // !!! IMPORTANT !!!
                    // If there are any parameters from the server
                    // then we'll use them to override anything in the JSON files
                    config.AddInMemoryCollection(bootConfig.Parameters);
                })
                .UseIISIntegration()
                .UseStartup<Startup>()
                .Build();

            return webHost;
        }
    }
}
