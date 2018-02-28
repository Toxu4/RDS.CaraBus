using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace RDS.CaraBus.Utility
{
    internal static class TypeExtensions
    {
        private static readonly ConcurrentDictionary<Type, IReadOnlyCollection<Type>> _typesCache = new ConcurrentDictionary<Type, IReadOnlyCollection<Type>>();

        public static IReadOnlyCollection<Type> GetInheritanceChainAndInterfaces(this Type type)
        {
            return _typesCache.GetOrAdd(type, GetInheritanceChainAndInterfacesImpl);
        }

        private static IReadOnlyCollection<Type> GetInheritanceChainAndInterfacesImpl(Type type)
        {
            var chain = new List<Type>();

            chain.AddRange(type.GetInterfaces());

            while (true)
            {
                if (type == null)
                {
                    break;
                }

                chain.Insert(0, type);
                type = type.BaseType;
            }

            return chain;
        }
    }
}