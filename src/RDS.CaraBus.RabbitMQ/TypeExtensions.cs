using System;
using System.Collections.Generic;
using System.Reflection;

namespace RDS.CaraBus.RabbitMQ
{
    internal static class TypeExtensions
    {
        public static List<Type> InheritanceChain(this Type type)
        {
            var chain = new List<Type>();

            while (true)
            {
                if (type == null)
                {
                    break;
                }

                chain.Insert(0, type);
                type = type.GetTypeInfo().BaseType;
            }

            return chain;
        }
    }
}