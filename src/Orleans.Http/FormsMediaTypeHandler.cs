using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Orleans.Http.Abstractions;

namespace Orleans.Http
{
    internal sealed class FormsMediaTypeHandler : IMediaTypeHandler
    {
        private static readonly Type _dicType = typeof(Dictionary<string, string>);
        public string[] MediaTypes => new[] { "application/x-www-form-urlencoded" };

        public async ValueTask<object> Deserialize(PipeReader reader, Type type, CancellationToken cancellationToken)
        {
            // For Forms we only accept Dictionary<string, string>
            if (type != _dicType) return default;

            var formsReader = new FormPipeReader(reader);

            var form = await formsReader.ReadFormAsync(cancellationToken);

            var model = new Dictionary<string, string>(form.Count);
            foreach (var kv in form)
            {
                model[kv.Key] = kv.Value;
            }

            return model;
        }

        public async ValueTask Serialize(object obj, PipeWriter writer)
        {
            if (obj.GetType() != _dicType) return;

            var content = new FormUrlEncodedContent((Dictionary<string, string>)obj);

            // We should find a way to avoid this copy
            await writer.WriteAsync((await content.ReadAsByteArrayAsync()).AsMemory());
        }
    }
}