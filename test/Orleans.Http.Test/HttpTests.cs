using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Orleans.Http.Test
{
    public class HttpTests : IClassFixture<HostFixture>
    {
        private readonly IHost _host;
        private readonly HttpClient _http;

        public HttpTests(HostFixture fixture)
        {
            this._host = fixture.Host;
            this._http = fixture.Http;
        }

        [Fact]
        public async Task RouteTest()
        {
            var url = "/grains";

            var response = await this._http.GetHttpMessage(TestExtensions.JSON, url);
            Assert.True(response.StatusCode == HttpStatusCode.NotFound);

            url = "/grains/test/Orleans.Http.Test.ITestGrain";
            response = await this._http.GetHttpMessage(TestExtensions.JSON, url);
            Assert.True(response.StatusCode == HttpStatusCode.NotFound);

            url = "/test/Orleans.Http.Test.ITestGrain/00000000-0000-0000-0000-000000000000/get";
            response = await this._http.GetHttpMessage(TestExtensions.JSON, url);
            Assert.True(response.StatusCode == HttpStatusCode.NotFound);

            url = "/Orleans.Http.Test.ITestGrain/00000000-0000-0000-0000-000000000000/get";
            response = await this._http.GetHttpMessage(TestExtensions.JSON, url);
            Assert.True(response.StatusCode == HttpStatusCode.NotFound);

            url = "/grains/Orleans.Http.Test.ITestGrain/00000000-0000-0000-0000-000000000000/get";
            response = await this._http.GetHttpMessage(TestExtensions.JSON, url);
            Assert.True(response.StatusCode == HttpStatusCode.NotFound);

            url = "/grains/test/Orleans.Http.Test.ITestGrain/get";
            response = await this._http.GetHttpMessage(TestExtensions.JSON, url);
            Assert.True(response.StatusCode == HttpStatusCode.NotFound);

            url = "/grains/test/Orleans.Http.Test.ITestGrain/00000000-0000-0000-0000-000000000000";
            response = await this._http.GetHttpMessage(TestExtensions.JSON, url);
            Assert.True(response.StatusCode == HttpStatusCode.NotFound);

            url = "/grains/test/Orleans.Http.Test.ITestGrain/00000000-0000-0000-0000-000000000000/get";
            response = await this._http.GetHttpMessage(TestExtensions.JSON, url);
            Assert.True(response.StatusCode == HttpStatusCode.OK);

            url = "/grains/test/Orleans.Http.Test.ITestGrain/00000000-0000-0000-0000-000000000000/post";
            response = await this._http.GetHttpMessage(TestExtensions.JSON, url);
            Assert.True(response.StatusCode == HttpStatusCode.MethodNotAllowed);

            url = "/grains/test/00000000-0000-0000-0000-000000000000/GetCustom";
            response = await this._http.GetHttpMessage(TestExtensions.JSON, url);
            Assert.True(response.StatusCode == HttpStatusCode.Created);
            Assert.True(response.Headers.GetValues("CustomHeader").First() == "HeaderValue");
            var payload = JsonSerializer.Deserialize<TestPayload>(await response.Content.ReadAsStringAsync());
            Assert.NotNull(payload);
            Assert.True(payload.Number == 1);
            Assert.True(payload.Text == nameof(IGrainHttpResult));
        }

        [Fact]
        public async Task PostTest()
        {
            var payload = new TestPayload();
            payload.Number = 12340000;
            payload.Text = "Test text";

            var url = "/grains/Test/Orleans.Http.Test.ITestGrain/00000000-0000-0000-0000-000000000000/post7";
            var responsePayload = await this._http.PostProtobuf<TestPayload, TestPayload>(url, payload);
            Assert.True(responsePayload.Number == payload.Number);
            Assert.True(responsePayload.Text == payload.Text);
        }

        [Fact]
        public async Task AuthTest()
        {
            var jwt = GetToken();

            var url = "/grains/test/Orleans.Http.Test.ITestGrain/00000000-0000-0000-0000-000000000000/GetWithAuth";
            var response = await this._http.GetHttpMessage(TestExtensions.JSON, url);
            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized);

            response = await this._http.GetHttpMessage(TestExtensions.JSON, url, jwt);
            Assert.True(response.StatusCode == HttpStatusCode.OK);
            var authResponse = JsonSerializer.Deserialize<AuthResponse>(await response.Content.ReadAsStringAsync());
            Assert.True(authResponse.Role == "user");
            Assert.True(authResponse.User == "TestUser");

            url = "/grains/test/Orleans.Http.Test.ITestGrain/00000000-0000-0000-0000-000000000000/GetWithAuthAdmin";
            response = await this._http.GetHttpMessage(TestExtensions.JSON, url, jwt);
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden);

            jwt = GetToken(true);
            response = await this._http.GetHttpMessage(TestExtensions.JSON, url, jwt);
            Assert.True(response.StatusCode == HttpStatusCode.OK);
            authResponse = JsonSerializer.Deserialize<AuthResponse>(await response.Content.ReadAsStringAsync());
            Assert.True(authResponse.Role == "admin");
            Assert.True(authResponse.User == "TestUser");
        }

        private static string GetToken(bool admin = false)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(Startup.SECRET);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.Name, "TestUser"),
                    new Claim(ClaimTypes.Role, admin ? "admin" : "user")
                }),
                Expires = DateTime.UtcNow.AddDays(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
