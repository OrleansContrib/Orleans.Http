using Microsoft.Extensions.DependencyInjection;
using Orleans.Http.Abstractions;
using Orleans.Http.MediaTypes.Protobuf;

namespace Orleans.Http
{
    public static class HostingExtensions
    {
        public static IServiceCollection AddProtobufMediaType(this IServiceCollection services)
        {
            return services
                .AddSingleton<IMediaTypeHandler, ProtobufMediaTypeHandler>();
        }
    }
}