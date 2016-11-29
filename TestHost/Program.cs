using Orleans;
using Orleans.Runtime.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TestGrains;

namespace TestHost
{
    class DevSilo : IDisposable
    {
        private static OrleansHostWrapper hostWrapper;

        public DevSilo()
        {
            // The Orleans silo environment is initialized in its own app domain in order to more
            // closely emulate the distributed situation, when the client and the server cannot
            // pass data via shared memory.
            AppDomain hostDomain = AppDomain.CreateDomain("OrleansHost", null, new AppDomainSetup
            {
                AppDomainInitializer = InitSilo,
                AppDomainInitializerArguments = new string[0],
            });
        }

        static void InitSilo(string[] args)
        {
            hostWrapper = new OrleansHostWrapper();

            if (!hostWrapper.Run())
            {
                Console.Error.WriteLine("Failed to initialize Orleans silo");
            }
        }

        public void Dispose()
        {
            if (hostWrapper == null) return;
            hostWrapper.Dispose();
            GC.SuppressFinalize(hostWrapper);
        }
    }


    class Program
    {
        static int testSize = 10;

        static void Main(string[] args)
        {
            using (var silo = new DevSilo())
            {
                Console.WriteLine("Web server running at http://localhost:8080/");

                // warm up
                Console.WriteLine("WARM UP");
                RunAllTests();

                // test
                Console.WriteLine("TESTS");
                testSize = 10000;
                RunAllTests();

                Console.WriteLine("Press ENTER to exit...");
                Console.ReadLine();
            }
        }

        private static void RunAllTests()
        {
            var grainOverhead = RunDirectTests();
            var httpTimeWithReflection = RunHttpTests("http://localhost:8080/grain/ITestGrain/0/Test/");
            var httpTimeWithPingGrain = RunHttpTests("http://localhost:8080/pinggrain");
            var httpTimeWithPing = RunHttpTests("http://localhost:8080/ping");

            // rough calculation
            Console.WriteLine($"Reflection overhead = {httpTimeWithReflection - httpTimeWithPingGrain} = {100 * (httpTimeWithReflection - httpTimeWithPingGrain) / httpTimeWithReflection}%");
        }

        static long RunDirectTests()
        {
            GrainClient.Initialize(ClientConfiguration.LocalhostSilo());

            var timer = Stopwatch.StartNew();
            for (var i = 0; i < testSize; i++)
            {
                var grain = GrainClient.GrainFactory.GetGrain<ITestGrain>("0");
                grain.Test().Wait();
            }
            timer.Stop();
            Console.WriteLine($"Time for direct connection tests: {timer.ElapsedMilliseconds}ms");
            return timer.ElapsedMilliseconds;
        }

        static long RunHttpTests(string url)
        {
            var timer = Stopwatch.StartNew();
            for (var i = 0; i < testSize; i++)
            {
                var request = WebRequest.Create(url);
                using (request.GetResponseAsync().Result)
                { }
            }

            timer.Stop();
            Console.WriteLine($"Time for HTTP connection tests: {timer.ElapsedMilliseconds}ms for {url}");
            return timer.ElapsedMilliseconds;
        }

    }
}
