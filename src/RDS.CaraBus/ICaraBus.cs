using System;
using System.Threading;
using System.Threading.Tasks;

namespace RDS.CaraBus
{
    public interface ICaraBus : IDisposable
    {
        Task StartAsync();

        Task PublishAsync(object message, PublishOptions options = null, CancellationToken cancellationToken = default(CancellationToken));

        Task SubscribeAsync(Type messageType, Func<object, CancellationToken, Task> handler, SubscribeOptions subscribeOptions = null, CancellationToken cancellationToken = default(CancellationToken));
    }
}