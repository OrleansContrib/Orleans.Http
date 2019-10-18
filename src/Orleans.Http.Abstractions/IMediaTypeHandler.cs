using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Orleans.Http.Abstractions
{
    public interface IMediaTypeHandler
    {
        string MediaType { get; }
        ValueTask Serialize(object obj, PipeWriter writer);
        ValueTask<object> Deserialize(PipeReader reader, Type type, CancellationToken cancellationToken);
    }
}