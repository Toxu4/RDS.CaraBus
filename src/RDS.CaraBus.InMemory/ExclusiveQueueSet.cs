using System;
using System.Collections.Concurrent;

namespace RDS.CaraBus.InMemory
{
    internal class ExclusiveQueueSet
    {
        private readonly ConcurrentDictionary<string, ExclusiveQueue> _groups = new ConcurrentDictionary<string, ExclusiveQueue>();

        public void Enqueue(object message)
        {
            foreach (var group in _groups.Values)
            {
                group.Enqueue(message);
            }
        }

        public void Subscribe(string exclusiveGroup, Action<object> subscriberAction)
        {
            _groups.AddOrUpdate(
                exclusiveGroup,
                group =>
                {
                    var queue = new ExclusiveQueue();
                    queue.Subscribe(subscriberAction);
                    return queue;
                },
                (group, queue) =>
                {
                    queue.Subscribe(subscriberAction);
                    return queue;
                });
        }
    }
}