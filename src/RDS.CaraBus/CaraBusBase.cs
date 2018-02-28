using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RDS.CaraBus.AsyncEx.Nito.AsyncEx.Coordination;
using RDS.CaraBus.AsyncEx.Nito.AsyncEx.SyncTasks;
using RDS.CaraBus.Utility;

namespace RDS.CaraBus
{
    public abstract class CaraBusBase<TOptions> : ICaraBus, ICarabusStopAsync where TOptions : CaraBusBaseOptions
    {
        protected readonly ILogger _logger;
        protected readonly TOptions _options;
        protected readonly TaskQueue _taskQueue;

        private bool _isRunning;
        private readonly ConcurrentBag<Func<Task>> _subscribeActions = new ConcurrentBag<Func<Task>>();
        private readonly AsyncLock _lock = new AsyncLock();

        protected CaraBusBase(TOptions options, TaskQueue taskQueue, ILoggerFactory loggerFactory = null)
        {
            _options = options;
            _taskQueue = taskQueue;
            _logger = loggerFactory?.CreateLogger(GetType()) ?? NullLogger<CaraBusBase<TOptions>>.Instance;

            if (options.AutoStart)
            {
                _isRunning = true;
            }
        }

        public Task PublishAsync(object message, PublishOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!_isRunning)
            {
                throw new CaraBusException("Should be running");
            }

            return PublishAsyncImpl(message, options ?? new PublishOptions(), cancellationToken);
        }

        protected abstract Task PublishAsyncImpl(object message, PublishOptions publishOptions, CancellationToken cancellationToken);

        public Task SubscribeAsync(Type messageType, Func<object, CancellationToken, Task> handler, SubscribeOptions subscribeOptions = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!_options.AutoStart && _isRunning)
            {
                throw new CaraBusException("Should be stopped");
            }

            Task SubFunc() => SubscribeAsyncImpl(messageType, handler, subscribeOptions ?? SubscribeOptions.NonExclusive(), cancellationToken);

            if (_options.AutoStart)
            {
                return SubFunc();
            }

            _subscribeActions.Add(SubFunc);

            return Task.CompletedTask;
        }

        protected abstract Task SubscribeAsyncImpl(Type messageType, Func<object, CancellationToken, Task> handler, SubscribeOptions subscribeOptions, CancellationToken cancellationToken);

        public async Task StartAsync()
        {
            if (_options.AutoStart)
            {
                throw new CaraBusException("Already runned by AutoStart option");
            }

            using (await _lock.LockAsync().ConfigureAwait(false))
            {
                try
                {
                    if (_isRunning)
                    {
                        throw new CaraBusException("Already running");
                    }

                    foreach (var subscribeAction in _subscribeActions)
                    {
                        await subscribeAction().ConfigureAwait(false);
                    }

                    _isRunning = true;
                }
                catch (Exception e)
                {
                    _logger?.LogError(e, "Cannot start");
                    throw;
                }
            }
        }

        public async Task StopAsync()
        {
            if (_isRunning)
            {
                using (await _lock.LockAsync().ConfigureAwait(false))
                {
                    if (_isRunning)
                    {
                        try
                        {
                            if (_options.TimeoutOnStop.HasValue && (_taskQueue.Queued > 0 || _taskQueue.Working > 0))
                            {
                                await Task.WhenAny(_taskQueue.WaitWhenEmpty, Task.Delay(_options.TimeoutOnStop.Value)).ConfigureAwait(false);
                            }

                            _isRunning = false;
                        }
                        catch (Exception e)
                        {
                            _logger?.LogError(e, "Cannot stop");
                            throw;
                        }
                    }
                }
            }
        }

        public virtual void Dispose()
        {
            StopAsync().WaitAndUnwrapException();
            _taskQueue?.Dispose();
        }
    }
}
