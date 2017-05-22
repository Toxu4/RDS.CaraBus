using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using RDS.CaraBus.Common;

namespace RDS.CaraBus.InMemory
{
    public class CaraBus : ICaraBus
    {
        private readonly PublishOptions _defaultPublishOptions = new PublishOptions();
        private readonly SubscribeOptions _defaultSubscribeOptions = new SubscribeOptions();

        private ConcurrentBag<Action> _subscribeActions = new ConcurrentBag<Action>();

        private readonly ConcurrentDictionary<(string scope, Type type), (NonExclusiveQueue nonExclusive, ExclusiveQueue exclusive)> _exchanges = 
            new ConcurrentDictionary<(string scope, Type type), (NonExclusiveQueue nonExclusive, ExclusiveQueue exclusive)>();

        private readonly ConcurrentDictionary<Type, IEnumerable<Type>> _typesCache = new ConcurrentDictionary<Type, IEnumerable<Type>>();

        private bool _isRunning;

        private readonly object _locker = new object();

        public bool IsRunning()
        {
            return _isRunning;
        }

        public void Start()
        {
            lock (_locker)
            {
                if (IsRunning())
                {
                    throw new CaraBusException("Already running");
                }

                foreach (var subscribeAction in _subscribeActions)
                {
                    subscribeAction.Invoke();
                }

                _isRunning = true;
            }
        }

        public void Stop()
        {
            lock (_locker)
            {
                if (!IsRunning())
                {
                    throw new CaraBusException("Already stopped");
                }

                _subscribeActions = new ConcurrentBag<Action>();
                _typesCache.Clear();

                _isRunning = false;
            }
        }

        public Task PublishAsync<T>(T message, PublishOptions options = null) where T : class
        {
            if (!IsRunning())
            {
                throw new CaraBusException("Should be running");
            }

            options = options ?? _defaultPublishOptions;

            var types = _typesCache
                .GetOrAdd(
                    message.GetType(), 
                    (mt) => mt 
                        .InheritanceChainAndInterfaces()
                        .Where(t => _exchanges.ContainsKey((options.Scope, t))));

            foreach (var type in types)
            {
                _exchanges[(options.Scope, type)].nonExclusive.Enqueue(message);
                _exchanges[(options.Scope, type)].exclusive.Enqueue(message);
            }

            return Task.CompletedTask;
        }

        public void Subscribe<T>(Action<T> handler, SubscribeOptions options = null) where T : class
        {
            if (IsRunning())
            {
                throw new CaraBusException("Should be stopped");
            }

            _subscribeActions.Add(() => InternalSubscribe(handler, options ?? _defaultSubscribeOptions));
        }

        private void InternalSubscribe<T>(Action<T> handler, SubscribeOptions options) where T : class
        {
            options = options ?? _defaultSubscribeOptions;

            var maxConcurrentHandlers = options.MaxConcurrentHandlers > 0 ? options.MaxConcurrentHandlers : (ushort)1;

            var semaphore = new SemaphoreSlim(maxConcurrentHandlers);

            void SubscriberAction(object message)
            {
                Task.Factory.StartNew(() =>
                {
                    semaphore.Wait();
                    try
                    {
                        handler.Invoke((T) message);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });
            }

            var (nonExclusive, exclusive) = _exchanges.GetOrAdd((options.Scope, typeof(T)), _ => (new NonExclusiveQueue(), new ExclusiveQueue()));
            if (options.Exclusive)
            {
                exclusive.Subscribe(SubscriberAction);
            }
            else
            {
                nonExclusive.Subscribe(SubscriberAction);
            }
        }
    }
}
