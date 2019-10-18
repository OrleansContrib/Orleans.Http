using System;
using System.IO.Pipelines;
using System.Threading.Tasks;
using Orleans.Http.Abstractions;
using System.Text.Json;
using System.Threading;

namespace Orleans.Http
{
    public sealed class JsonMediaTypeHandler : IMediaTypeHandler
    {
        // TODO: Allow customization
        private static readonly JsonSerializerOptions _settings = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        private const string MEDIA_TYPE = "application/json";
        public string MediaType => MEDIA_TYPE;

        public async ValueTask<object> Deserialize(PipeReader reader, Type type, CancellationToken cancellationToken)
        {
            object model = default;

            while (!cancellationToken.IsCancellationRequested)
            {
                var readResult = await reader.ReadAsync(cancellationToken);
                var buffer = readResult.Buffer;

                model = JsonSerializer.Deserialize(buffer.FirstSpan, type, _settings);

                reader.AdvanceTo(buffer.Start, buffer.End);

                if (readResult.IsCompleted) break;
            }

            return model;
        }

        public async ValueTask Serialize(object obj, PipeWriter writer)
        {
            await JsonSerializer.SerializeAsync(writer.AsStream(), obj, obj.GetType(), _settings);
        }
    }
}