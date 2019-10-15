using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Orleans.Http
{
    [Serializable]
    [DataContract]
    public class MethodCall
    {
        [DataMember]
        [JsonProperty("to")]
        public GrainIdentity Target { get; set; }

        [DataMember]
        public long Id { get; set; }

        [DataMember]
        [JsonProperty("method")]
        public string MethodName { get; set; }

        [DataMember]
        [JsonProperty("args")]
        public object[] Arguments { get; set; }
        
        public T Arg<T>(int arg = 0)
        {
            if (arg > this.Arguments.Length)
            {
                throw new IndexOutOfRangeException("Tried to get argument " + arg + ", but only have " + this.Arguments.Length + " args.");
            }

            var value = this.Arguments[arg];

            if (value is null) return default;

            if (value is T variable)
            {
                return variable;
            }

            if (value is JToken token)
            {
                return token.ToObject<T>();
            }

            return JToken.FromObject(value).ToObject<T>();
        }

        public object Arg(Type type, int arg = 0)
        {
            if (arg > this.Arguments.Length)
            {
                throw new IndexOutOfRangeException("Tried to get argument " + arg + ", but only have " + this.Arguments.Length + " args.");
            }

            var value = this.Arguments[arg];
            if (value == null)
            {
                return null;
            }

            if (value.GetType() == type)
            {
                return value;
            }

            if (value is JToken token)
            {
                return token.ToObject(type);
            }

            return JToken.FromObject(value).ToObject(type);
        }

        public override string ToString()
        {
            var result = new StringBuilder($"Id: {this.Id}");
            
            if (this.Target != null)
            {
                result.Append($", Target: {this.Target}");
            }

            if (!string.IsNullOrWhiteSpace(this.MethodName))
            {
                result.Append($", MethodName: {this.MethodName}");
            }

            if (this.Arguments != null && this.Arguments.Length > 0)
            {
                result.Append($", Args: {string.Join(", ", this.Arguments.Select(_ => _ == null ? "null" : _.ToString()))}");
            }

            return result.ToString();
        }
    }
}