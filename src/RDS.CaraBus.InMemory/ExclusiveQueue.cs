using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace RDS.CaraBus.InMemory
{
    internal class ExclusiveQueue : AbstractQueue
    {
        private readonly ConcurrentQueue<Action<object>> _subscribersActions = new ConcurrentQueue<Action<object>>();

        protected override void NotifySubscribers(object message)
        {
            if (!_subscribersActions.TryDequeue(out var subscriberAction))
            {
                return;
            }
                
            Task.Factory.StartNew(() =>
            {
                subscriberAction.Invoke(message);
            });

            _subscribersActions.Enqueue(subscriberAction);
        }

        public override void Subscribe(Action<object> subscriberAction)
        {
            _subscribersActions.Enqueue(subscriberAction);
        }
    }
}