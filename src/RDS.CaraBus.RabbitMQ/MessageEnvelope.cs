using System;
using Newtonsoft.Json;

namespace RDS.CaraBus.RabbitMQ
{
    internal class MessageEnvelope
    {
        public Type Type { get; set; }
        public string Data { get; set; }

        public MessageEnvelope()
        {
        }

        public MessageEnvelope(object data, JsonSerializerSettings jsonSerializerSettings)
        {
            Type = data.GetType();
            Data = JsonConvert.SerializeObject(data, jsonSerializerSettings);
        }
    }
}
