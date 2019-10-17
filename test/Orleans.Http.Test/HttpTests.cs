using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
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
            this._host.Start();
        }
    }
}
