using System;
using System.Reflection;
using Newtonsoft.Json;

namespace RDS.CaraBus.RabbitMQ
{
    internal class TypeJsonConverter : JsonConverter
    {
        private static readonly TypeInfo typeTypeInfo = typeof(Type).GetTypeInfo();

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is Type type)
            {
                writer.WriteValue(TypeEx.GetShortAssemblyQualifiedName(type));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return objectType != typeof(Type) ? null : TypeEx.ToTypeName((string)reader.Value);
        }

        public override bool CanConvert(Type objectType)
        {
            return typeTypeInfo.IsAssignableFrom(objectType);
        }
    }
}
