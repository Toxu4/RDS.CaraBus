using System;
using System.Threading.Tasks;

namespace RDS.CaraBus
{
    public interface ICaraBus
    {
        bool IsRunning();

        void Start();
        void Stop();

        Task PublishAsync<T>(T message, PublishOptions options = null) where T : class;
        void Subscribe<T>(Func<T, Task> handler, SubscribeOptions options = null) where T : class;
    }
}