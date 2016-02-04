using Microsoft.Owin.Hosting;
using Orleans;
using Orleans.Providers;
using Orleans.Runtime;
using System;
using System.Threading.Tasks;

namespace OrleansHttp
{
    public class Bootstrap : IBootstrapProvider
    {
        IDisposable host;
        Logger logger;
      
        public string Name { get; private set; }


        public Task Close()
        {
            host.Dispose();
            return TaskDone.Done;
        }


        public Task Init(string name, IProviderRuntime providerRuntime, IProviderConfiguration config)
        {
            this.logger = providerRuntime.GetLogger(name);
            this.Name = name;

            var router = new Router();
            new GrainController(router, TaskScheduler.Current,  providerRuntime);

            var options = new StartOptions
            {
                ServerFactory = "Nowin",
                Port = config.Properties.ContainsKey("Port") ? int.Parse(config.Properties["Port"]) : 8080,
            };

            var username = config.Properties.ContainsKey("Username") ? config.Properties["Username"] : null;
            var password = config.Properties.ContainsKey("Password") ? config.Properties["Password"] : null;

            host = WebApp.Start(options, app => new WebServer(router, username, password).Configure(app));

            this.logger.Verbose($"HTTP API listening on {options.Port}");

            return Task.FromResult(0);
        }
    }
}
