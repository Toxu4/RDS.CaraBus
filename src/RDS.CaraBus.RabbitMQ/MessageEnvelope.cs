using System;

namespace RDS.CaraBus.RabbitMQ
{
    internal class MessageEnvelope
    {
        public Type Type { get; set; }

        public string Data { get; set; }
    }
}
