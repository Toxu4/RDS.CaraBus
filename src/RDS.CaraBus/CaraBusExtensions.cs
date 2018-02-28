using System;
using System.Threading;
using System.Threading.Tasks;

namespace RDS.CaraBus
{
    public static class CaraBusExtensions
    {
        public static void Subscribe<T>(this ICaraBus caraBus, Action<T> handler, SubscribeOptions options = null) where T : class
        {
            caraBus.SubscribeAsync(typeof(T), (message, token) =>
            {
                handler((T) message);
                return Task.CompletedTask;
            }, options).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public static void Subscribe(this ICaraBus caraBus, Type messageType, Action<object> handler, SubscribeOptions options = null)
        {
            caraBus.SubscribeAsync(messageType, (message, token) =>
            {
                handler(message);
                return Task.CompletedTask;
            }, options).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public static void Subscribe<T>(this ICaraBus caraBus, Func<T, CancellationToken, Task> handler, SubscribeOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
        {
            caraBus.SubscribeAsync(typeof(T), (message, token) => handler((T) message, token), options, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public static void Subscribe<T>(this ICaraBus caraBus, Func<T, Task> handler, SubscribeOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
        {
            caraBus.SubscribeAsync(typeof(T), (message, token) => handler((T)message), options, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public static void Subscribe(this ICaraBus caraBus, Type messageType, Func<object, CancellationToken, Task> handler, SubscribeOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            caraBus.SubscribeAsync(messageType, handler, options, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public static Task SubscribeAsync<T>(this ICaraBus caraBus, Func<T, CancellationToken, Task> handler, SubscribeOptions options = null, CancellationToken cancellationToken = default(CancellationToken)) where T : class
        {
            return caraBus.SubscribeAsync(typeof(T), (message, token) => handler((T) message, token), options, cancellationToken);
        }

        public static Task StopAsync(this ICaraBus caraBus)
        {
            if (caraBus is ICarabusStopAsync carabusStopAsync)
            {
                return carabusStopAsync.StopAsync();
            }

            return Task.CompletedTask;
        }
    }
}
