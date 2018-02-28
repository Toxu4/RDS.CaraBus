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
using Microsoft.Extensions.Logging;
using RDS.CaraBus.AsyncEx.Nito.AsyncEx.Coordination;
using RDS.CaraBus.AsyncEx.Nito.AsyncEx.SyncTasks;
using RDS.CaraBus.Utility;

namespace RDS.CaraBus.RabbitMQ
{
    public class RabbitMQCaraBus : CaraBusBase<RabbitMQCaraBusOptions>
    {
        private readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new TypeJsonConverter() }
        };

        private readonly ConcurrentDictionary<Type, string> _typeNames = new ConcurrentDictionary<Type, string>();
        private readonly IConnectionFactory _connectionFactory;

        private IConnection _connection;
        private IModel _publishChannel;
        private ConcurrentBag<IModel> _subscribeChannels = new ConcurrentBag<IModel>();

        private readonly AsyncLock _lock = new AsyncLock();

        public RabbitMQCaraBus(RabbitMQCaraBusOptions rabbitMQCaraBusOptions, ILoggerFactory loggerFactory = null)
            : base(
                rabbitMQCaraBusOptions,
                new TaskQueue(maxItems: 100, maxDegreeOfParallelism: rabbitMQCaraBusOptions.MaxDegreeOfParallelism, loggerFactory: loggerFactory), 
                loggerFactory)
        {
            _connectionFactory = new ConnectionFactory {Uri = new Uri(rabbitMQCaraBusOptions.ConnectionString)};
        }

        private async Task EnsureConnectionAndPublishChannelCreated()
        {
            if (_connection == null)
            {
                using (await _lock.LockAsync().ConfigureAwait(false))
                {
                    if (_connection == null)
                    {
                        _connection = _connectionFactory.CreateConnection();
                    }
                }
            }

            if (_publishChannel == null)
            {
                using (await _lock.LockAsync().ConfigureAwait(false))
                {
                    if (_publishChannel == null)
                    {
                        _publishChannel = _connection.CreateModel();
                    }
                }
            }
        }

        protected override async Task SubscribeAsyncImpl(Type messageType, Func<object, CancellationToken, Task> handler, SubscribeOptions subscribeOptions, CancellationToken cancellationToken)
        {
            await EnsureConnectionAndPublishChannelCreated().ConfigureAwait(false);

            var exchangeName = GetExchangeName(subscribeOptions.Scope, messageType);
            var queueName = GetQueueName(messageType, subscribeOptions);

            var channel = _connection.CreateModel();

            channel.ExchangeDeclare(exchangeName, ExchangeType.Fanout, durable: true, autoDelete: true);
            channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: !subscribeOptions.IsExclusive);
            channel.QueueBind(queueName, exchangeName, string.Empty);

            var singleAckPerChannel = new AsyncLock();

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                bool TryEnqueue() => _taskQueue.Enqueue(async () =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        var bodyString = Encoding.UTF8.GetString(ea.Body);

                        try
                        {
                            var envelope = JsonConvert.DeserializeObject<MessageEnvelope>(bodyString, _jsonSerializerSettings);
                            var message = JsonConvert.DeserializeObject(envelope.Data, envelope.Type, _jsonSerializerSettings);

                            await handler(message, cancellationToken).ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(e, $"Error while handling message: {bodyString}");
                        }

                        using (await singleAckPerChannel.LockAsync().ConfigureAwait(false))
                        {
                            try
                            {
                                channel.BasicAck(ea.DeliveryTag, false);
                            }
                            catch (Exception e)
                            {
                                _logger.LogError(e, "Error while sending Ack");
                            }
                        }
                    }
                });

                while (!cancellationToken.IsCancellationRequested && !TryEnqueue())
                {
                    Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken).WaitAndUnwrapException(cancellationToken);
                }
            };

            channel.BasicConsume(queueName, false, consumer);

            _subscribeChannels.Add(channel);
        }

        protected override async Task PublishAsyncImpl(object message, PublishOptions publishOptions, CancellationToken cancellationToken)
        {
            await EnsureConnectionAndPublishChannelCreated().ConfigureAwait(false);

            var messageType = message.GetType();

            var serializedEnvelope = JsonConvert.SerializeObject(
                new MessageEnvelope
                {
                    Type = messageType,
                    Data = JsonConvert.SerializeObject(message, _jsonSerializerSettings)
                },
                _jsonSerializerSettings);

            var buffer = Encoding.UTF8.GetBytes(serializedEnvelope);

            var types = messageType.GetInheritanceChainAndInterfaces();

            foreach (var type in types)
            {
                var exchangeName = GetExchangeName(publishOptions.Scope, type);
                using (await _lock.LockAsync().ConfigureAwait(false))
                {
                    _publishChannel.ExchangeDeclare(exchangeName, ExchangeType.Fanout, durable: true, autoDelete: true);
                    _publishChannel.BasicPublish(exchangeName, string.Empty, null, buffer);
                }
            }
        }
        public override void Dispose()
        {
            base.Dispose();
            CloseChannels();
            CloseConnection();
        }

        private void CloseChannels()
        {
            using (_lock.Lock())
            {
                if (_publishChannel != null && _publishChannel.IsOpen)
                {
                    _publishChannel.Close();
                }

                _publishChannel?.Dispose();
                _publishChannel = null;

                while (_subscribeChannels.TryTake(out var subscribeChannel))
                {
                    if (subscribeChannel != null && subscribeChannel.IsOpen)
                    {
                        subscribeChannel.Close();
                    }

                    subscribeChannel?.Dispose();
                }

                _subscribeChannels = null;
            }
        }

        private void CloseConnection()
        {
            using (_lock.Lock())
            {
                if (_connection != null && _connection.IsOpen)
                {
                    _connection.Close();
                }

                _connection?.Dispose();
                _connection = null;
            }
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

                return $"{type.Name}|{GetHash(type)}";
            }
        }

        private string GetHash(Type type)
        {
            using (var algorithm = SHA256.Create())
            {
                var hash = algorithm.ComputeHash(Encoding.ASCII.GetBytes(type.GetTypeInfo().Assembly.GetName().Name + "|" + type.FullName));

                var hashString = string.Empty;
                foreach (byte x in hash)
                {
                    hashString += $"{x:x2}";
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

        private string GetQueueName(Type messageType, SubscribeOptions options)
        {
            const int maxQueueNameLength = 255;

            var typeName = GetTypeName(messageType);
            var typePostfix = options.IsExclusive ? options.ExclusiveGroup : Guid.NewGuid().ToString();

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