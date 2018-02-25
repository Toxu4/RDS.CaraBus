using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using RDS.CaraBus.Utility;

namespace RDS.CaraBus.InMemory
{
    internal class VirtualExchange
    {
        private readonly TaskQueue _taskQueue;

        private readonly ConcurrentBag<Func<object, Task>> _nonExclusiveSubs = new ConcurrentBag<Func<object, Task>>();

        private readonly ConcurrentDictionary<string, RoundRobinList> _exclusiveGroups = new ConcurrentDictionary<string, RoundRobinList>();

        public VirtualExchange(TaskQueue taskQueue)
        {
            _taskQueue = taskQueue;
        }

        public void Notify(object message)
        {
            foreach (var nonExclusiveSub in _nonExclusiveSubs)
            {
                _taskQueue.Enqueue(() => nonExclusiveSub(message));
            }

            foreach (var exclusiveGroup in _exclusiveGroups)
            {
                var exclusiveSub = exclusiveGroup.Value.Next();

                if (exclusiveSub != null)
                {
                    _taskQueue.Enqueue(() => exclusiveSub(message));
                }
            }
        }

        public void SubscribeExclusive(string name, Func<object, Task> handler)
        {
            _exclusiveGroups.AddOrUpdate(
                name,
                group =>
                {
                    var list = new RoundRobinList();
                    list.Add(handler);
                    return list;
                },
                (group, subs) =>
                {
                    subs.Add(handler);
                    return subs;
                });
        }

        public void SubscribeNonExclusive(Func<object, Task> handler)
        {
            _nonExclusiveSubs.Add(handler);
        }

        private class RoundRobinList
        {
            private readonly ConcurrentQueue<Func<object, Task>> _queue = new ConcurrentQueue<Func<object, Task>>();

            public void Add(Func<object, Task> handler)
            {
                lock (_queue)
                {
                    _queue.Enqueue(handler);
                }
            }

            public Func<object, Task> Next()
            {
                Func<object, Task> subscriberAction;

                lock (_queue)
                {
                    if (_queue.TryDequeue(out subscriberAction))
                    {
                        _queue.Enqueue(subscriberAction);
                    }
                }

                return subscriberAction;
            }
        }
    }
}
