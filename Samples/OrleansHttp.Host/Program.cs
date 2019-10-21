using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Hosting;
using OrleansHttp.Grains;

namespace OrleansHttp.Host
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var hostBuilder = new HostBuilder();
            hostBuilder.UseConsoleLifetime();
            hostBuilder.ConfigureLogging(logging => logging.AddConsole());
            hostBuilder.ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseUrls("http://*:9090");
                webBuilder.UseStartup<Startup>();
            });

            hostBuilder.UseOrleans(b =>
            {
                b.UseLocalhostClustering();

                b.ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(HelloGrain).Assembly).WithReferences());
            });

            var host = hostBuilder.Build();
            await host.StartAsync();

            Console.ReadLine();
        }
    }
}
