using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans.Http.Abstractions;

namespace Orleans.Http
{

    internal sealed class MediaTypeManager
    {
        private readonly ILogger _logger;
        private readonly Dictionary<string, IMediaTypeHandler> _handlers;

        public MediaTypeManager(IServiceProvider serviceProvider)
        {
            this._logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger<MediaTypeManager>();
            this._handlers = serviceProvider.GetServices<IMediaTypeHandler>()?.ToDictionary(h => h.MediaType, h => h, StringComparer.InvariantCultureIgnoreCase);
            if (this._handlers == null || this._handlers.Count == 0)
            {
                this._logger.LogWarning("There are no IMediaTypeHandlers registered! Request body will be ignored.");
                this._handlers = new Dictionary<string, IMediaTypeHandler>();
            }
        }

        public async ValueTask<bool> Serialize(string mediaType, object obj, PipeWriter writer)
        {
            IMediaTypeHandler handler = default;
            try
            {
                if (this._handlers.TryGetValue(mediaType, out handler))
                {
                    await handler.Serialize(obj, writer);
                    return true;
                }
            }
            catch (Exception exc)
            {
                this._logger.LogWarning(exc, $"Failure to serialize body into '{handler.MediaType}' using {handler.GetType().FullName}: {exc.Message}.");
            }

            return false;
        }

        public ValueTask<object> Deserialize(string mediaType, PipeReader reader, Type type, CancellationToken cancellationToken)
        {
            IMediaTypeHandler handler = default;
            try
            {
                if (this._handlers.TryGetValue(mediaType, out handler))
                {
                    return handler.Deserialize(reader, type, cancellationToken);
                }
            }
            catch (Exception exc)
            {
                this._logger.LogWarning(exc, $"Failure to deserialize body into '{handler.MediaType}' using {handler.GetType().FullName}: {exc.Message}.");
            }

            return default;
        }
    }
}