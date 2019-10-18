using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using ProtoBuf;
using Xunit;

namespace Orleans.Http.Test
{
    public class HttpTests : IClassFixture<HostFixture>
    {
        private readonly IHost _host;

        public HttpTests(HostFixture fixture)
        {
            this._host = fixture.Host;
        }

        [Fact]
        public async Task EndToEnd()
        {
            var payload = new TestPayload();
            payload.Number = 12340000;
            payload.Text = "Test text";
            using (var file = File.Create("payload.bin"))
            {
                Serializer.Serialize(file, payload);
            }
            this._host.Start();
            Console.ReadLine();
        }
    }
}
