using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RDS.CaraBus.AsyncEx.Nito.AsyncEx.Coordination;

namespace RDS.CaraBus.Utility
{
    public class TaskQueue : IDisposable
    {
        private readonly ConcurrentQueue<Func<Task>> _queue = new ConcurrentQueue<Func<Task>>();
        private readonly SemaphoreSlim _semaphore;
        private readonly AsyncAutoResetEvent _autoResetEvent = new AsyncAutoResetEvent();
        private TaskCompletionSource<bool> _tscQueue = new TaskCompletionSource<bool>();

        private CancellationTokenSource _workLoopCancellationTokenSource;
        private readonly int _maxItems;
        private int _working;
        private readonly ILogger _logger;

        public TaskQueue(int maxItems = Int32.MaxValue, bool autoStart = true, byte maxDegreeOfParallelism = 1, ILoggerFactory loggerFactory = null)
        {
            _maxItems = maxItems;
            _semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
            _logger = loggerFactory?.CreateLogger<TaskQueue>() ?? NullLogger<TaskQueue>.Instance;

            if (autoStart)
            {
                Start();
            }
        }

        public int Queued => _queue.Count;
        public int Working => _working;

        public Task WaitWhenEmpty => _tscQueue.Task;

        public bool Enqueue(Func<Task> task)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            if (_queue.Count >= _maxItems)
            {
                _logger.LogDebug("Ignoring queued task: Queue is full");
                return false;
            }

            _queue.Enqueue(task);
            _autoResetEvent.Set();
            return true;
        }

        public void Start(CancellationToken token = default(CancellationToken))
        {
            _workLoopCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            StartWorking();
        }

        private void StartWorking()
        {
            bool isTraceLogLevelEnabled = _logger.IsEnabled(LogLevel.Trace);
            if (isTraceLogLevelEnabled) _logger.LogTrace("Starting worker loop.");
            Task.Run(async () => {
                while (!_workLoopCancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        bool workerAvailable = await _semaphore.WaitAsync(1000, _workLoopCancellationTokenSource.Token).ConfigureAwait(false);
                        if (!_queue.TryDequeue(out var task))
                        {
                            if (workerAvailable)
                                _semaphore.Release();

                            if (_queue.IsEmpty)
                            {
                                if (isTraceLogLevelEnabled) _logger.LogTrace("Waiting to dequeue task.");
                                try
                                {
                                    using (var timeoutCancellationTokenSource = new CancellationTokenSource(10000))
                                    using (var dequeueCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_workLoopCancellationTokenSource.Token, timeoutCancellationTokenSource.Token))
                                    {
                                        await _autoResetEvent.WaitAsync(dequeueCancellationTokenSource.Token).ConfigureAwait(false);
                                    }
                                }
                                catch (OperationCanceledException) { }
                            }

                            continue;
                        }

                        Interlocked.Increment(ref _working);
                        if (isTraceLogLevelEnabled) _logger.LogTrace("Running dequeued task");
                        // TODO: Cancel after x amount of time.
                        var unawaited = Task.Run(() => task(), _workLoopCancellationTokenSource.Token)
                            .ContinueWith(t => {
                                Interlocked.Decrement(ref _working);
                                _semaphore.Release();

                                if (t.IsFaulted)
                                {
                                    var ex = t.Exception.InnerException;
                                    _logger.LogError(ex, "Error running dequeue task: {Message}", ex?.Message);
                                }
                                else if (t.IsCanceled)
                                {
                                    _logger.LogWarning("Dequeue task was cancelled.");
                                }
                                else if (isTraceLogLevelEnabled)
                                {
                                    _logger.LogTrace("Finished running dequeued task.");
                                }

                                if (_working == 0 && _queue.IsEmpty && _queue.Count == 0)
                                {
                                    if (isTraceLogLevelEnabled) _logger.LogTrace("Running completed action..");
                                    // NOTE: There could be a race here where an a semaphore was taken but the queue was empty.
                                    var _oldQueue = Interlocked.Exchange(ref _tscQueue, new TaskCompletionSource<bool>());
                                    _oldQueue.TrySetResult(true);
                                }
                            });
                    }
                    catch (OperationCanceledException ex)
                    {
                        _logger.LogWarning(ex, "Worker loop was cancelled.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error running worker loop: {Message}", ex.Message);
                    }
                }
            }, _workLoopCancellationTokenSource.Token)
                .ContinueWith(t => {
                    if (t.IsFaulted)
                    {
                        var ex = t.Exception.InnerException;
                        _logger.LogError(ex, "Worker loop exiting: {Message}", ex.Message);
                    }
                    else if (t.IsCanceled || _workLoopCancellationTokenSource.Token.IsCancellationRequested)
                    {
                        _logger.LogTrace("Worker loop was cancelled.");
                    }
                    else
                    {
                        _logger.LogCritical("Worker loop finished prematurely.");
                    }

                    if (!_workLoopCancellationTokenSource.Token.IsCancellationRequested)
                        StartWorking();
                });
        }

        public void Dispose()
        {
            _logger.LogTrace("Disposing");
            _workLoopCancellationTokenSource?.Cancel();

            //queue = clear
            while (_queue.TryDequeue(out var result))
            {
            }
        }
    }
}