using System;
using System.Threading.Tasks;

namespace RDS.CaraBus
{
    public interface ICaraBus
    {
        bool IsRunning();

        Task StartAsync();
        Task StopAsync(StopOptions options = null);

        Task PublishAsync<T>(T message, PublishOptions options = null) where T : class;
        void Subscribe<T>(Func<T, Task> handler, SubscribeOptions options = null) where T : class;
        void Subscribe<T>(Action<T> handler, SubscribeOptions options = null) where T : class;

        void Subscribe(Type messageType, Func<object, Task> handler, SubscribeOptions options = null);
        void Subscribe(Type messageType, Action<object> handler, SubscribeOptions options = null);
    }
}