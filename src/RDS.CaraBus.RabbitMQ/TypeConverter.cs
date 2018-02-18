using System;
using System.Reflection;
using Newtonsoft.Json;

namespace RDS.CaraBus.RabbitMQ
{
    public class TypeSerializer : JsonConverter
    {

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var v = value as Type;
            if (v == null)
            {
                return;
            }
            writer.WriteValue(TypeEx.GetShortAssemblyQualifiedName(v));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (objectType != typeof(Type))
            {
                return null;
            }

            return TypeEx.ToTypeName((string)reader.Value);
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(Type).GetTypeInfo().IsAssignableFrom(objectType);
        }
    }
}
