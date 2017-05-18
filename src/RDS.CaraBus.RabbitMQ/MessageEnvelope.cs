using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace RDS.CaraBus.RabbitMQ
{
    internal class MessageEnvelope
    {
        public List<Type> InheritanceChain { get; set; }
        public List<Type> Interfaces { get; set; }

        public string Data { get; set; }

        public MessageEnvelope()
        {
            
        }

        public MessageEnvelope(object data)
        {
            Data = JsonConvert.SerializeObject(data);
            Interfaces = data.GetType().GetInterfaces().ToList();
            InheritanceChain = data.GetType().InheritanceChain();
        }
    }
}
