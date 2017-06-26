using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RDS.CaraBus
{
    public class HandlerPool
    {
        private readonly ConcurrentDictionary<int, Task> _currentTasks = new ConcurrentDictionary<int, Task>();

        public Task Handle(Func<Task> handler)
        {
            var task = Task.Factory.StartNew(async () =>
            {
                await handler.Invoke();
            });

            _currentTasks.TryAdd(task.Id, task);
            task.ContinueWith(o => _currentTasks.TryRemove(task.Id, out Task value));

            return task;
        }

        public void Stop(TimeSpan timeout)
        {
            var task = _currentTasks.Select(t => t.Value).ToArray();
            Task.WaitAll(task, timeout);
        }
    }
}
