using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace RDS.CaraBus.InMemory
{
    internal class NonExclusiveQueue : AbstractQueue
    {
        private readonly ConcurrentBag<Action<object>> _subscribersActions = new ConcurrentBag<Action<object>>();

        protected override void NotifySubscribers(object message)
        {
            foreach (var subscriberAction in _subscribersActions)
            {
                Task.Factory.StartNew(() =>
                {
                    subscriberAction.Invoke(message);
                });
            }
        }

        public override void Subscribe(Action<object> subscriberAction)
        {
            _subscribersActions.Add(subscriberAction);
        }
    }
}