using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace RDS.CaraBus.InMemory
{
    internal class ExclusiveQueue : AbstractQueue
    {
        private readonly ConcurrentQueue<Action<object>> _subscribersActions = new ConcurrentQueue<Action<object>>();

        private readonly object _locker = new object();

        protected override void NotifySubscribers(object message)
        {
            Action<object> subscriberAction;
            lock (_locker)
            {
                if (!_subscribersActions.TryDequeue(out subscriberAction))
                {
                    return;
                }

                _subscribersActions.Enqueue(subscriberAction);
            }

            Task.Run(() =>
            {
                subscriberAction.Invoke(message);
            });
        }

        public override void Subscribe(Action<object> subscriberAction)
        {
            _subscribersActions.Enqueue(subscriberAction);
        }
    }
}