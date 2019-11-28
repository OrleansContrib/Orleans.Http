using System;
using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using ProtoBuf;
using System.Text.Json;
using System.IO;
using System.Text;

namespace Orleans.Http.Test
{
    public class HostFixture : IDisposable
    {
        public IHost Host { get; private set; }
        public HttpClient Http { get; private set; }

        public HostFixture()
        {
            this.Run();
        }

        public void Run()
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
            this.Host.Start();
            this.Http = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:9090")
            };
        }

        public void Dispose()
        {
            this.Host.StopAsync().Wait();
            this.Host.Dispose();
        }
    }

    public static class TestExtensions
    {
        public const string PROTOBUF = "application/protobuf";
        public const string JSON = "application/json";

        public static Task<HttpResponseMessage> GetHttpMessage(this HttpClient http, string mimeType, string path, string token = "")
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(mimeType));
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            return http.SendAsync(request);
        }

        public static async Task<T> GetProtobuf<T>(this HttpClient http, string path)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(PROTOBUF));
            var response = await http.SendAsync(request);
            var responseBodyStream = await response.Content.ReadAsStreamAsync();
            var responsePayload = Serializer.Deserialize<T>(responseBodyStream);
            return responsePayload;
        }

        public static async Task<T> GetJSON<T>(this HttpClient http, string path)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(JSON));
            var response = await http.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            var responsePayload = JsonSerializer.Deserialize<T>(responseBody);
            return responsePayload;
        }

        public static async Task<O> PostProtobuf<I, O>(this HttpClient http, string path, I input)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path);
            using var inputStream = new MemoryStream();
            Serializer.Serialize(inputStream, input);
            request.Content = new ByteArrayContent(inputStream.ToArray());
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(PROTOBUF);
            var response = await http.SendAsync(request);
            var responseBodyStream = await response.Content.ReadAsStreamAsync();
            if (responseBodyStream.Length > 0)
            {
                var responsePayload = Serializer.Deserialize<O>(responseBodyStream);
                return responsePayload;
            }
            return default;
        }

        public static async Task<O> PostJSON<I, O>(this HttpClient http, string path, I input)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Content = new StringContent(JsonSerializer.Serialize(input, typeof(I)), Encoding.UTF8, JSON);
            var response = await http.SendAsync(request);
            var responseBodyStream = await response.Content.ReadAsStreamAsync();
            if (responseBodyStream.Length > 0)
            {
                var responsePayload = Serializer.Deserialize<O>(responseBodyStream);
                return responsePayload;
            }
            return default;
        }
    }
}