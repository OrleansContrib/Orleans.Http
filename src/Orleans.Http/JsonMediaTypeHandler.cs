using System;
using System.IO.Pipelines;
using System.Threading.Tasks;
using Orleans.Http.Abstractions;
using System.Text.Json;
using System.Threading;

namespace Orleans.Http
{
    internal sealed class JsonMediaTypeHandler : IMediaTypeHandler
    {
        private readonly JsonSerializerOptions _options;
        public string[] MediaTypes => new[] { "application/json; charset=utf-8", "application/json;charset=utf-8", "application/json" };

        public JsonMediaTypeHandler(JsonSerializerOptions options)
        {
            this._options = options;
        }

        public async ValueTask<object> Deserialize(PipeReader reader, Type type, CancellationToken cancellationToken)
        {
            object model = default;

            while (!cancellationToken.IsCancellationRequested)
            {
                var readResult = await reader.ReadAsync(cancellationToken);
                var buffer = readResult.Buffer;

                model = JsonSerializer.Deserialize(buffer.FirstSpan, type, _options);

                reader.AdvanceTo(buffer.Start, buffer.End);

                if (readResult.IsCompleted) break;
            }

            return model;
        }

        public async ValueTask Serialize(object obj, PipeWriter writer)
        {
            await JsonSerializer.SerializeAsync(writer.AsStream(), obj, obj.GetType(), _options);
        }
    }
}