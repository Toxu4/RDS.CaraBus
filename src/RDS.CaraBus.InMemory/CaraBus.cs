using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RDS.CaraBus.InMemory
{
    public class CaraBus : ICaraBus
    {
        private readonly PublishOptions _defaultPublishOptions = new PublishOptions();
        private readonly SubscribeOptions _defaultSubscribeOptions = new SubscribeOptions();

        private ConcurrentBag<Action> _subscribeActions = new ConcurrentBag<Action>();

        private readonly ConcurrentDictionary<(string scope, Type type), (NonExclusiveQueue nonExclusive, ExclusiveQueue
            exclusive)> _exchanges =
            new ConcurrentDictionary<(string scope, Type type), (NonExclusiveQueue nonExclusive, ExclusiveQueue
                exclusive)>();

        private readonly ConcurrentDictionary<Type, IEnumerable<Type>> _typesCache =
            new ConcurrentDictionary<Type, IEnumerable<Type>>();

        private readonly ConcurrentDictionary<int, Task> _currentTasks = new ConcurrentDictionary<int, Task>();

        private bool _isRunning;

        private readonly SemaphoreSlim _runningSemaphore = new SemaphoreSlim(1);

        public bool IsRunning()
        {
            return _isRunning;
        }

        public async Task StartAsync()
        {
            await _runningSemaphore.WaitAsync();

            try
            {
                if (IsRunning())
                {
                    throw new CaraBusException("Already running");
                }

                foreach (var subscribeAction in _subscribeActions)
                {
                    subscribeAction();
                }

                _isRunning = true;
            }
            finally
            {
                _runningSemaphore.Release();
            }
        }

        public async Task StopAsync(StopOptions options = null)
        {
            options = options ?? new StopOptions();

            await _runningSemaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                if (!IsRunning())
                {
                    throw new CaraBusException("Already stopped");
                }

                if (options.WaitForSubscribers && !_currentTasks.IsEmpty)
                {
                    var task = _currentTasks.Select(t => t.Value).ToArray();
                    await Task.WhenAny(Task.WhenAll(task), Task.Delay(options.Timeout)).ConfigureAwait(false);
                }

                _subscribeActions = new ConcurrentBag<Action>();
                _typesCache.Clear();

                _isRunning = false;
            }
            finally
            {
                _runningSemaphore.Release();
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
                    mt => mt
                        .GetInheritanceChainAndInterfaces()
                        .Where(t => _exchanges.ContainsKey((options.Scope, t))));

            foreach (var type in types)
            {
                _exchanges[(options.Scope, type)].nonExclusive.Enqueue(message);
                _exchanges[(options.Scope, type)].exclusive.Enqueue(message);
            }

            return Task.CompletedTask;
        }

        public void Subscribe<T>(Func<T, Task> handler, SubscribeOptions options = null) where T : class
        {
            async Task Handler(object m)
            {
                await handler((T)m);
            }

            Subscribe(typeof(T), Handler, options);
        }
        public void Subscribe(Type messageType, Func<object, Task> handler, SubscribeOptions options = null)
        {
            if (IsRunning())
            {
                throw new CaraBusException("Should be stopped");
            }

            _subscribeActions.Add(() => InternalSubscribe(messageType, handler, options ?? _defaultSubscribeOptions));
        }

        public void Subscribe<T>(Action<T> handler, SubscribeOptions options = null) where T : class
        {
            Task Handler(object m)
            {
                handler((T)m);
                return Task.CompletedTask;
            }

            Subscribe(typeof(T), Handler, options);
        }

        public void Subscribe(Type messageType, Action<object> handler, SubscribeOptions options = null)
        {
            if (IsRunning())
            {
                throw new CaraBusException("Should be stopped");
            }

            Task Handler(object m)
            {
                handler(m);
                return Task.CompletedTask;
            }

            _subscribeActions.Add(() => InternalSubscribe(messageType, Handler, options ?? _defaultSubscribeOptions));
        }

        private void InternalSubscribe(Type messageType, Func<object, Task> handler, SubscribeOptions options)
        {
            options = options ?? _defaultSubscribeOptions;

            var maxConcurrentHandlers = options.MaxConcurrentHandlers > 0 ? options.MaxConcurrentHandlers : (ushort)1;

            var semaphore = new SemaphoreSlim(maxConcurrentHandlers);

            void SubscriberAction(object message)
            {
                var task = Task.Run(async () =>
                {
                    await semaphore.WaitAsync().ConfigureAwait(false);

                    try
                    {
                        await handler(message).ConfigureAwait(false);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                _currentTasks.TryAdd(task.Id, task);
                task.ContinueWith(o =>
                {
                    _currentTasks.TryRemove(task.Id, out Task value);
                });
            }

            var (nonExclusive, exclusive) = _exchanges.GetOrAdd((options.Scope, messageType),
                _ => (new NonExclusiveQueue(), new ExclusiveQueue()));
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
