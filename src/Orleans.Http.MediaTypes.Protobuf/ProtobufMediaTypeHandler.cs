using System;
using System.Threading;
using Orleans.Http.Abstractions;
using System.Threading.Tasks;
using System.IO.Pipelines;
using ProtoBuf;

namespace Orleans.Http.MediaTypes.Protobuf
{
    internal sealed class ProtobufMediaTypeHandler : IMediaTypeHandler
    {
        public string MediaType => "application/protobuf";

        public ValueTask<object> Deserialize(PipeReader reader, Type type, CancellationToken cancellationToken)
        {
            var model = Serializer.Deserialize(type, reader.AsStream());

            return new ValueTask<object>(model);
        }

        public ValueTask Serialize(object obj, PipeWriter writer)
        {
            Serializer.Serialize(writer.AsStream(), obj);
            return default;
        }
    }
}
