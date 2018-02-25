using System;

namespace RDS.CaraBus
{
    public class SubscribeOptions
    {
        private SubscribeOptions(string scope, string exclusiveGroup)
        {
            Scope = scope;
            ExclusiveGroup = exclusiveGroup;
        }

        public string Scope { get; set; }

        public string ExclusiveGroup { get; }

        public bool IsExclusive => ExclusiveGroup != null;

        public static SubscribeOptions Exclusive(Action<SubscribeOptions> configure = null)
        {
            var opt = new SubscribeOptions(null, "Exclusive");

            configure?.Invoke(opt);

            return opt;
        }

        public static SubscribeOptions Exclusive(string exclusiveGroup, Action<SubscribeOptions> configure = null)
        {
            var opt = new SubscribeOptions(null, exclusiveGroup);

            configure?.Invoke(opt);

            return opt;
        }

        public static SubscribeOptions NonExclusive(Action<SubscribeOptions> configure = null)
        {
            var opt = new SubscribeOptions(null, null);

            configure?.Invoke(opt);

            return opt;
        }

    }
}