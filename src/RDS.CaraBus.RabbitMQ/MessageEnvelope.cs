using System;
using Newtonsoft.Json;

namespace RDS.CaraBus.RabbitMQ
{
    internal class MessageEnvelope
    {
        public MessageEnvelope()
        {

        }

        public MessageEnvelope(object data)
        {
            TypeString = TypeEx.GetShortAssemblyQualifiedName(data.GetType());
            Data = JsonConvert.SerializeObject(data);
        }

        [JsonProperty("Type")]
        public string TypeString { get; set; }

        [JsonProperty("Data")]
        public string Data { get; set; }

        [JsonIgnore]
        public Type Type => TypeEx.ToTypeName(TypeString);
    }
}
