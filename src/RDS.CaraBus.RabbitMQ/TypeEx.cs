using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace RDS.CaraBus.RabbitMQ
{
    internal static class TypeEx
    {
        private static readonly ConcurrentDictionary<string, Type> _stringTypeNameLookup = new ConcurrentDictionary<string, Type>(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<Type, string> _typeStringNameLookup = new ConcurrentDictionary<Type, string>();

        static readonly Regex SubtractFullNameRegex = new Regex(@", Version=\d+.\d+.\d+.\d+, Culture=\w+, PublicKeyToken=\w+", RegexOptions.Compiled);

        private static readonly string CoreAssemblyName = GetCoreRuntimeAssemblyName();

        private static string GetCoreRuntimeAssemblyName()
        {
            var name = 1.GetType().AssemblyQualifiedName;
            var part = name.Substring(name.IndexOf(",", StringComparison.Ordinal));
            return part;
        }

        public static string GetShortAssemblyQualifiedName(Type self)
        {
            return _typeStringNameLookup.GetOrAdd(self, type =>
            {
                var full = type.AssemblyQualifiedName.Replace(CoreAssemblyName, ",%core%"); ;

                var shortened = SubtractFullNameRegex.Replace(full, string.Empty);
                if (Type.GetType(shortened, false) == null)
                {
                    // if type cannot be found with shortened name - use full name
                    shortened = full;
                }

                _stringTypeNameLookup.TryAdd(shortened, type); //add to reverse cache

                return shortened;
            });
        }

        public static Type ToTypeName(string shortAssemblyQualifiedName)
        {
            return _stringTypeNameLookup.GetOrAdd(shortAssemblyQualifiedName, name =>
            {
                var full = name.Replace(",%core%", CoreAssemblyName);

                var type = Type.GetType(SubtractFullNameRegex.Replace(full, string.Empty), false);
                if (type == null)
                {
                    // if type cannot be found with shortened name - use full name
                    type = Type.GetType(full); //throw exception if null
                }

                _typeStringNameLookup.TryAdd(type, shortAssemblyQualifiedName); //add to reverse cache

                return type;
            });
        }
    }
}
