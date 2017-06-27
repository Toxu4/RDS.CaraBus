using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace RDS.CaraBus.RabbitMQ
{
    public class CaraBus : ICaraBus, IDisposable
    {
        private readonly PublishOptions _defaultPublishOptions = new PublishOptions();
        private readonly SubscribeOptions _defaultSubscribeOptions = new SubscribeOptions();

        private readonly ConcurrentBag<Action> _subscribeActions = new ConcurrentBag<Action>();
        private readonly ConcurrentDictionary<Type, IEnumerable<Type>> _typesCache = new ConcurrentDictionary<Type, IEnumerable<Type>>();
        private readonly ConcurrentDictionary<int, Task> _currentTasks = new ConcurrentDictionary<int, Task>();
        private readonly ConcurrentDictionary<Type, string> _typeNames =
            new ConcurrentDictionary<Type, string>();

        private bool _isRunning;

        private readonly IConnectionFactory _connectionFactory;

        private IConnection _connection;
        private IModel _publishChannel;

        private const string DefaultQueueName = "RDS.CaraBus.DefaultQueue|faiujf09834fyh2f87y29x87yjxf";

        private readonly SemaphoreSlim _runningSemaphore = new SemaphoreSlim(1);

        public CaraBus(IConnectionFactory connectionFactory = null)
        {
            _connectionFactory = connectionFactory ?? CreateDefaultConnectionFactory("localhost");
        }

        public CaraBus(Action<ConnectionFactory> configureConnectionFactory)
        {
            var connectionFactory = CreateDefaultConnectionFactory("localhost");

            configureConnectionFactory?.Invoke(connectionFactory);

            _connectionFactory = connectionFactory;
        }

        private static ConnectionFactory CreateDefaultConnectionFactory(string hostName)
        {
            return new ConnectionFactory
            {
                HostName = hostName,
                AutomaticRecoveryEnabled = true,
                RequestedHeartbeat = 10
            };
        }

        public bool IsRunning()
        {
            return _isRunning;
        }
        public async Task StartAsync()
        {
            await _runningSemaphore.WaitAsync();

            try
            {
                if (IsRunning())
                {
                    throw new CaraBusException("Already running");
                }

                _connection = _connectionFactory.CreateConnection();
                _publishChannel = _connection.CreateModel();

                _publishChannel.QueueDeclare(DefaultQueueName, durable: true, exclusive: false);
                _publishChannel.BasicConsume(DefaultQueueName, true, new EventingBasicConsumer(_publishChannel));

                foreach (var subscribeAction in _subscribeActions)
                {
                    subscribeAction.Invoke();
                }

                _isRunning = true;
            }
            finally
            {
                _runningSemaphore.Release();
            }
        }

        public async Task StopAsync(StopOptions options = null)
        {
            options = options ?? new StopOptions();

            await _runningSemaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                if (!IsRunning())
                {
                    throw new CaraBusException("Already stopped");
                }

                CloseAndDisposeConnection();

                if (options.WaitForSubscribers && !_currentTasks.IsEmpty)
                {
                    var task = _currentTasks.Select(t => t.Value).ToArray();
                    await Task.WhenAny(Task.WhenAll(task), Task.Delay(options.Timeout)).ConfigureAwait(false);
                }

                _isRunning = false;
            }
            finally
            {
                _runningSemaphore.Release();
            }
        }

        public Task PublishAsync<T>(T message, PublishOptions options = null) where T : class
        {
            if (!IsRunning())
            {
                throw new CaraBusException("Should be running");
            }

            options = options ?? _defaultPublishOptions;

            var envelope = new MessageEnvelope(message);
            var serializedEnvelope = JsonConvert.SerializeObject(envelope);
            var buffer = Encoding.UTF8.GetBytes(serializedEnvelope);

            var types = _typesCache.GetOrAdd(message.GetType(), mt => mt.GetInheritanceChainAndInterfaces());

            foreach (var type in types)
            {
                var exchangeName = GetExchangeName(options.Scope, type);
                _publishChannel.ExchangeDeclare(exchangeName, "fanout", durable: true, autoDelete: true);
                _publishChannel.QueueBind(DefaultQueueName, exchangeName, string.Empty);
                _publishChannel.BasicPublish(exchangeName, "", null, buffer);
            }

            return Task.CompletedTask;
        }

        public void Subscribe<T>(Func<T, Task> handler, SubscribeOptions options = null) where T : class
        {
            if (IsRunning())
            {
                throw new CaraBusException("Should be stopped");
            }

            _subscribeActions.Add(() => InternalSubscribe(handler, options ?? _defaultSubscribeOptions));
        }

        public void Subscribe<T>(Action<T> handler, SubscribeOptions options = null) where T : class
        {
            if (IsRunning())
            {
                throw new CaraBusException("Should be stopped");
            }

            Task FakeTask(T m)
            {
                handler(m);
                return Task.CompletedTask;
            }

            _subscribeActions.Add(() => InternalSubscribe((Func<T, Task>)FakeTask, options ?? _defaultSubscribeOptions));
        }

        private void InternalSubscribe<T>(Func<T, Task> handler, SubscribeOptions options) where T : class
        {
            var exchangeName = GetExchangeName(options.Scope, typeof(T));
            var queueName = GetQueueName<T>(options);

            var maxConcurrentHandlers = options.MaxConcurrentHandlers > 0 ? options.MaxConcurrentHandlers : (ushort)1;

            var channel = _connection.CreateModel();

            channel.BasicQos(0, maxConcurrentHandlers, true);
            channel.ExchangeDeclare(exchangeName, "fanout", durable: true, autoDelete: true);
            channel.QueueDeclare(queueName, durable: true, exclusive: false);
            channel.QueueBind(queueName, exchangeName, string.Empty);

            var semaphore = new SemaphoreSlim(maxConcurrentHandlers);

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var task = Task.Run(async () =>
                {
                    await semaphore.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        var bodyString = Encoding.UTF8.GetString(ea.Body);
                        var envelope = JsonConvert.DeserializeObject<MessageEnvelope>(bodyString);
                        var message = JsonConvert.DeserializeObject(envelope.Data, envelope.Type);

                        await handler((T)message).ConfigureAwait(false);
                    }
                    finally
                    {
                        channel.BasicAck(ea.DeliveryTag, false);
                        semaphore.Release();
                    }
                });

                _currentTasks.TryAdd(task.Id, task);
                task.ContinueWith(o =>
                {
                    _currentTasks.TryRemove(task.Id, out Task value);
                });
            };

            channel.BasicConsume(queueName, false, consumer);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            if (_connection != null)
            {
                CloseAndDisposeConnection();
            }
        }

        public void CloseAndDisposeConnection()
        {
            if (_connection.IsOpen)
            {
                _connection.Close();
            }

            _connection.Dispose();
        }

        private string GetTypeName(Type type)
        {
            return _typeNames.GetOrAdd(type, GetTypeNameInternal);

            string GetTypeNameInternal(Type internalType)
            {
                if (type.IsConstructedGenericType)
                {
                    var arguments = type.GetGenericArguments().Select(a => a.Name);
                    var formattedArguments = string.Join(", ", arguments);

                    var namespacePrefix = string.Empty;
                    if (type.DeclaringType != null)
                    {
                        namespacePrefix = $"{type.DeclaringType.Name}";
                    }

                    return $"{namespacePrefix}+{type.Name}[{formattedArguments}]|{GetHash(type)}";
                }
                else
                {
                    return $"{type.Name}|{GetHash(type)}";
                }
            }
        }

        private string GetHash(Type type)
        {
            using (var algorithm = SHA256.Create())
            {
                var hash = algorithm.ComputeHash(Encoding.ASCII.GetBytes(type.AssemblyQualifiedName));

                string hashString = string.Empty;
                foreach (byte x in hash)
                {
                    hashString += String.Format("{0:x2}", x);
                }

                return hashString;
            }
        }


        private string GetExchangeName(string scope, Type type)
        {
            const int maxExchangeNameLength = 255;

            var typeName = GetTypeName(type);

            var exchangeName = $"{scope}|{typeName}";

            if (exchangeName.Length > maxExchangeNameLength)
            {
                var extraQueueNameLength = exchangeName.Length - maxExchangeNameLength;
                var lengthToRemove = extraQueueNameLength <= typeName.Length ? extraQueueNameLength : typeName.Length;
                exchangeName = $"{scope}|{typeName.Remove(0, lengthToRemove)}";
            }

            return exchangeName;
        }

        private string GetQueueName<T>(SubscribeOptions options)
        {
            const int maxQueueNameLength = 255;

            var typeName = GetTypeName(typeof(T));
            var typePostfix = options.Exclusive ? "Exclusive" : Guid.NewGuid().ToString();

            var queueName = $"{options.Scope}|{typeName}|{typePostfix}";

            if (queueName.Length > maxQueueNameLength)
            {
                var extraQueueNameLength = queueName.Length - maxQueueNameLength;
                var lengthToRemove = extraQueueNameLength <= typeName.Length ? extraQueueNameLength : typeName.Length;
                return $"{options.Scope}|{typeName.Remove(0, lengthToRemove)}|{typePostfix}";
            }

            return queueName;
        }
    }
}
