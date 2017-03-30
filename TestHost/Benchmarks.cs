using BenchmarkDotNet.Attributes;
using Orleans;
using Orleans.Runtime.Configuration;
using System.Net;
using TestGrains;

namespace TestHost
{
    public class Benchmarks
    {
        private WebRequest request;

        public Benchmarks()
        {
            GrainClient.Initialize(ClientConfiguration.LocalhostSilo());
        }


        [Benchmark]
        public void DirectConnection()
        {
            var grain = GrainClient.GrainFactory.GetGrain<ITestGrain>("0");
            grain.Test().Wait();
        }

        [Benchmark]
        public void HttpConnection()
        {
            this.request = WebRequest.Create("http://localhost:8080/grain/ITestGrain/0/Test/");
            using (request.GetResponseAsync().Result)
            { }
        }

    }
}
