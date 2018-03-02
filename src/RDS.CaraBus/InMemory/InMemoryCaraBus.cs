using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RDS.CaraBus.Utility;

namespace RDS.CaraBus.InMemory
{
    public class InMemoryCaraBus : CaraBusBase<InMemoryCaraBusOptions>
    {
        private readonly ConcurrentDictionary<(string scope, Type type), VirtualExchange> _exchanges = new ConcurrentDictionary<(string scope, Type type), VirtualExchange>();

        public InMemoryCaraBus(InMemoryCaraBusOptions inMemoryCaraBusOptions, ILoggerFactory loggerFactory = null) :
            base(inMemoryCaraBusOptions,
                new TaskQueue(maxDegreeOfParallelism: inMemoryCaraBusOptions.MaxDegreeOfParallelism, loggerFactory: loggerFactory), 
                loggerFactory)
        {
        }

        protected override Task PublishAsyncImpl(object message, PublishOptions publishOptions, CancellationToken cancellationToken)
        {
            var types = message.GetType().GetInheritanceChainAndInterfaces().Where(t => _exchanges.ContainsKey((publishOptions.Scope, t)));

            foreach (var type in types)
            {
                _exchanges[(publishOptions.Scope, type)].Notify(message);
            }

            return Task.CompletedTask;
        }

        protected override Task SubscribeAsyncImpl(Type messageType, Func<object, CancellationToken, Task> handler, SubscribeOptions subscribeOptions, CancellationToken cancellationToken)
        {
            async Task Handler(object message)
            {
                try
                {
                    await handler(message, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Error while handling message: {message}");
                }
            }

            var exchange = _exchanges.GetOrAdd((subscribeOptions.Scope, messageType), _ => new VirtualExchange(_taskQueue));
            if (subscribeOptions.IsExclusive)
            {
                exchange.SubscribeExclusive(subscribeOptions.ExclusiveGroup, Handler);
            }
            else
            {
                exchange.SubscribeNonExclusive(Handler);
            }

            return Task.CompletedTask;
        }
    }
}