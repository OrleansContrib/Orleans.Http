using System;
using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Orleans.Http.Test
{
    public class HostFixture : IDisposable
    {
        public IHost Host { get; private set; }
        public HostFixture()
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

                b.ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(Startup).Assembly));
            });

            this.Host = hostBuilder.Build();
        }
        public void Dispose()
        {
            this.Host.Dispose();
        }
    }
}