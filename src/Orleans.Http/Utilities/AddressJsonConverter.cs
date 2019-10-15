using System;
using Newtonsoft.Json;

namespace Orleans.Http.Utilities
{
    /// <summary>
    /// Converter for <see cref="GrainIdentity"/> objects.
    /// </summary>
    internal class AddressJsonConverter : JsonConverter
    {
        /// <inheritdoc />
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(((GrainIdentity)value).ToString() ?? string.Empty);
        }

        /// <inheritdoc />
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return GrainIdentity.FromString(reader.Value as string);
        }

        /// <inheritdoc />
        public override bool CanConvert(Type objectType)
        {
            return typeof(GrainIdentity) == objectType;
        }
    }
}