using System;
using System.Collections.Generic;
using System.Reflection;

namespace RDS.CaraBus
{
    internal static class TypeExtensions
    {
        public static List<Type> GetInheritanceChainAndInterfaces(this Type type)
        {

            var chain = new List<Type>();

#if NETSTANDARD1_5
            chain.AddRange(type.GetTypeInfo().GetInterfaces());
#else
            chain.AddRange(type.GetInterfaces());
#endif

            while (true)
            {
                if (type == null)
                {
                    break;
                }

                chain.Insert(0, type);
#if NETSTANDARD1_5
                type = type.GetTypeInfo().BaseType;
#else
                type = type.BaseType;
#endif
            }

            return chain;
        }
    }
}