using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace RDS.CaraBus.InMemory
{
    internal abstract class AbstractQueue
    {
        private readonly ManualResetEvent _started = new ManualResetEvent(false);
        private readonly AutoResetEvent _enqueued = new AutoResetEvent(false);

        private readonly ConcurrentQueue<object> _queue = new ConcurrentQueue<object>();

        protected AbstractQueue()
        {
            new Thread(() =>
            {
                _started.Set();

                while (true)
                {
                    _enqueued.WaitOne();
                    while (_queue.TryDequeue(out var message))
                    {
                        NotifySubscribers(message);
                    }
                }    
            }).Start();
        }

        public void Enqueue(object message)
        {
            _started.WaitOne();

            _queue.Enqueue(message);
            _enqueued.Set();
        }

        public abstract void Subscribe(Action<object> subscriberAction);
        protected abstract void NotifySubscribers(object message);
    }
}