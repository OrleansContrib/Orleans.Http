using System;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Orleans.Http.Abstractions;

namespace Orleans.Http
{
    internal sealed class XMLMediaTypeHandler : IMediaTypeHandler
    {
        private readonly ConcurrentDictionary<string, XmlSerializer> _serializers = new ConcurrentDictionary<string, XmlSerializer>();
        public string MediaType => "application/xml";

        public ValueTask<object> Deserialize(PipeReader reader, Type type, CancellationToken cancellationToken)
        {
            XmlSerializer serializer = this.GetSerializer(type);
            var model = serializer.Deserialize(reader.AsStream());
            return new ValueTask<object>(model);
        }

        public ValueTask Serialize(object obj, PipeWriter writer)
        {
            XmlSerializer serializer = this.GetSerializer(obj.GetType());
            serializer.Serialize(writer.AsStream(), obj);
            return default;
        }

        private XmlSerializer GetSerializer(Type type)
        {
            XmlSerializer serializer = default;
            if (!this._serializers.TryGetValue(type.Name, out serializer))
            {
                serializer = new XmlSerializer(type);
                this._serializers.AddOrUpdate(type.Name, name => serializer, (name, s) => s);
            }

            return serializer;
        }
    }
}