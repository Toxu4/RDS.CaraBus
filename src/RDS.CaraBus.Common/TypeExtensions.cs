using System;
using System.Collections.Generic;
using System.Reflection;

namespace RDS.CaraBus.Common
{
    public static class TypeExtensions
    {
        public static List<Type> InheritanceChainAndInterfaces(this Type type)
        {

            var chain = new List<Type>();

#if NET462
            chain.AddRange(type.GetInterfaces());
#endif
#if NETSTANDARD1_6
            chain.AddRange(type.GetTypeInfo().GetInterfaces());
#endif

            while (true)
            {
                if (type == null)
                {
                    break;
                }

                chain.Insert(0, type);
#if NET462
                type = type.BaseType;
#endif
#if NETSTANDARD1_6
                type = type.GetTypeInfo().BaseType;
#endif
            }

            return chain;
        }
    }
}