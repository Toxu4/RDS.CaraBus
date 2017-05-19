using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;

namespace RDS.CaraBus.RabbitMQ
{
    public class CaraBus : ICaraBus, IDisposable
    {
        private readonly Options _options;
        private readonly PublishOptions _defaultPublishOptions = new PublishOptions();
        private readonly SubscribeOptions _defaultSubscribeOptions = new SubscribeOptions();

        private ConcurrentBag<Action> _subscribeActions = new ConcurrentBag<Action>();

        private bool _isRunning;

        private readonly Lazy<IConnection> _connection;
        private readonly Lazy<IModel> _publishChannel;

        private const string _defaultQueueName = "RDS.CaraBus.DefaultQueue|faiujf09834fyh2f87y29x87yjxf";

        private Object locker = new Object();

        public CaraBus(Options options = null)
        {
            _options = options ?? new Options();

            _connection = new Lazy<IConnection>( () =>
                new ConnectionFactory
                {
                    HostName = _options.HostName
                }.CreateConnection());

            _publishChannel = new Lazy<IModel>( () =>
            {
                var channel = _connection.Value.CreateModel();

                channel.QueueDeclare(_defaultQueueName, durable: true, exclusive: false);
                channel.BasicConsume(_defaultQueueName, true, new EventingBasicConsumer(channel));

                return channel;
            });
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

            foreach (var type in envelope.InheritanceChain.Union(envelope.Interfaces))
            {
                var exchangeName = $"{options.Scope}|{type.FullName}";
                _publishChannel.Value.ExchangeDeclare(exchangeName, "fanout", durable: true, autoDelete: true);
                _publishChannel.Value.QueueBind(_defaultQueueName, exchangeName, string.Empty);
                _publishChannel.Value.BasicPublish(exchangeName, "", null, buffer);
            }

            return Task.CompletedTask;
        }

        public void Subscribe<T>(Action<T> handler, SubscribeOptions options = null) where T : class
        {
            if (IsRunning())
            {
                throw new CaraBusException("Should be stopped");
            }

            _subscribeActions.Add(() => InternalSubscribe(handler, options ?? _defaultSubscribeOptions));
        }

        private void InternalSubscribe<T>(Action<T> handler, SubscribeOptions options) where T : class
        {            
            var exchangeName = $"{options.Scope}|{typeof(T).FullName}";
            var queueName = $"{options.Scope}|{typeof(T).FullName}|{ (options.Exclusive ? "Exclusive" : Guid.NewGuid().ToString()) }";
            var maxConcurrentHandlers = options.MaxConcurrentHandlers > 0 ? options.MaxConcurrentHandlers : (ushort)1;

            var channel = _connection.Value.CreateModel();

            channel.BasicQos(0, maxConcurrentHandlers, true);        
            channel.ExchangeDeclare(exchangeName, "fanout", durable:true, autoDelete: true);
            channel.QueueDeclare(queueName, durable:true, exclusive: false);
            channel.QueueBind(queueName, exchangeName, string.Empty);

            var semaphore = new SemaphoreSlim(maxConcurrentHandlers);

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                Task.Factory.StartNew(() =>
                {
                    semaphore.Wait();
                    try
                    {
                        Received(channel, handler, ea);
                    }
                    finally
                    {
                        semaphore.Release();
                    }                    
                });
            };
            channel.BasicConsume(queueName, false, consumer);
        }

        private static void Received<T>(IModel channel, Action<T> handler, BasicDeliverEventArgs ea) where T : class
        {
            var bodyString = Encoding.UTF8.GetString(ea.Body);
            var envelope = JsonConvert.DeserializeObject<MessageEnvelope>(bodyString);
            var message = JsonConvert.DeserializeObject(envelope.Data, envelope.InheritanceChain.Last());

            handler((T)message);

            channel.BasicAck(ea.DeliveryTag, false);
        }


        public void Start()
        {
            lock (locker)
            {
                if (IsRunning())
                {
                    throw new CaraBusException("Already running");
                }

                foreach (var subscribeAction in _subscribeActions)
                {
                    subscribeAction.Invoke();
                }

                _isRunning = true;
            }
        }

        public void Stop()
        {
            lock (locker)
            {
                if (!IsRunning())
                {
                    throw new CaraBusException("Already stopped");
                }

                _subscribeActions = new ConcurrentBag<Action>();

                _isRunning = false;
            }
        }

        public bool IsRunning()
        {
            return _isRunning;
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

            if (_connection.IsValueCreated)
            {
                _connection.Value.Close();
                _connection.Value.Dispose();
            }
        }
    }
}
