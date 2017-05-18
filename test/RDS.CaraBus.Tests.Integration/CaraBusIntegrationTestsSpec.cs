using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RDS.CaraBus.Tests.Integration.Messages;

namespace RDS.CaraBus.Tests.Integration
{
    public abstract class CaraBusIntegrationTestsSpec
    {
        protected abstract ICaraBus CreateCaraBus();

        private ICaraBus _sut;

        [SetUp]
        public void SetUp()
        {
            _sut = CreateCaraBus();
        }

        [TearDown]
        public void TearDown()
        {
            _sut.Stop();
        }

        [Test]
        public async Task PublishSubscribe_ShouldDeliverMessage()
        {
            // given           
            var sentValue = Guid.NewGuid().ToString();
            var receivedValue = string.Empty;

            var delivered = new ManualResetEvent(false);

            _sut.Subscribe<TestMessage>(m =>
            {
                receivedValue = m.Value;
                delivered.Set();
            });

            _sut.Start();

            // when
            await _sut.PublishAsync(new TestMessage { Value = sentValue });

            // then
            delivered.WaitOne(TimeSpan.FromSeconds(2));

            Assert.That(receivedValue, Is.EqualTo(sentValue));
        }

        [Test]
        public async Task PublishSubscribe_ShouldDeliverMessage_IfSubscibedOnInterface()
        {
            // given
            var sentValue = Guid.NewGuid().ToString();
            var receivedValue = string.Empty;

            var delivered = new ManualResetEvent(false);

            _sut.Subscribe<ITestMessage>(m =>
            {
                receivedValue = ((TestMessage)m).Value;
                delivered.Set();
            });

            _sut.Start();

            // when
            await _sut.PublishAsync(new TestMessage { Value = sentValue });

            // then
            delivered.WaitOne(TimeSpan.FromSeconds(2));

            Assert.That(receivedValue, Is.EqualTo(sentValue));
        }

        [Test]
        public async Task PublishSubscribe_ShouldDeliverMessage_IfSubscibedOnParentMessageType()
        {
            // given
            var sentValue = Guid.NewGuid().ToString();
            var receivedValue = string.Empty;

            var delivered = new ManualResetEvent(false);

            _sut.Subscribe<TestMessage>(m =>
            {
                receivedValue = ((TestMessageDescendant)m).Value;
                delivered.Set();
            });

            _sut.Start();

            // when
            await _sut.PublishAsync(new TestMessageDescendant { Value = sentValue });

            // then
            delivered.WaitOne(TimeSpan.FromSeconds(2));

            Assert.That(receivedValue, Is.EqualTo(sentValue));
        }

        [Test]
        public async Task PublishSubscribe_ShouldDeliverToAllSubscribers_IfDeaultSubscriptionOptionsUsed()
        {
            // given
            var sentValue = Guid.NewGuid().ToString();
            var firstReceivedValue = string.Empty;
            var secondReceivedValue = string.Empty;

            var delivered = new CountdownEvent(2);

            _sut.Subscribe<TestMessage>(m =>
            {
                firstReceivedValue = m.Value;
                delivered.Signal();
            });

            _sut.Subscribe<TestMessage>(m =>
            {
                secondReceivedValue = m.Value;
                delivered.Signal();
            });

            _sut.Start();

            // when
            await _sut.PublishAsync(new TestMessage { Value = sentValue });

            // then
            delivered.Wait(TimeSpan.FromSeconds(2));

            Assert.That(firstReceivedValue, Is.EqualTo(sentValue));
            Assert.That(secondReceivedValue, Is.EqualTo(sentValue));
        }

        [Test]
        public async Task PublishSubscribe_ShouldDeliverToOneSubscriber_IfSubscriptionIsExclusive()
        {
            // given
            var sentValue = Guid.NewGuid().ToString();

            var deliveryCount = 0;

            var options = new SubscribeOptions { Exclusive = true };

            _sut.Subscribe<TestMessage>(m =>
            {
                Interlocked.Increment(ref deliveryCount);
            }, options);

            _sut.Subscribe<TestMessage>(m =>
            {
                Interlocked.Increment(ref deliveryCount);
            }, options);

            _sut.Start();

            // when
            await _sut.PublishAsync(new TestMessage { Value = sentValue });

            // then
            Thread.Sleep(TimeSpan.FromSeconds(2));

            Assert.That(deliveryCount, Is.EqualTo(1));
        }

        [Test]
        public void PublishSubscribe_ShouldDeliverMessagesInsideScopeOnly()
        {
            // given
            var scope1Name = Guid.NewGuid().ToString();
            var scope1SentValue = "[scope1value]";
            var scope1ReceivedValue = string.Empty;

            var scope2Name = Guid.NewGuid().ToString();
            var scope2SentValue = "[scope2value]";
            var scope2ReceivedValue = string.Empty;

            var delivered = new CountdownEvent(4);

            _sut.Subscribe<TestMessage>(m =>
            {
                scope1ReceivedValue = $"{scope1ReceivedValue}{m.Value}";
                delivered.Signal();
            }, new SubscribeOptions { Scope = scope1Name });

            _sut.Subscribe<TestMessage>(m =>
            {
                scope2ReceivedValue = $"{scope2ReceivedValue}{m.Value}";
                delivered.Signal();
            }, new SubscribeOptions { Scope = scope2Name });

            _sut.Start();

            // when
            var publish1_1 = _sut.PublishAsync(new TestMessage { Value = scope1SentValue }, new PublishOptions { Scope = scope1Name });
            var publish1_2 = _sut.PublishAsync(new TestMessage { Value = scope1SentValue }, new PublishOptions { Scope = scope1Name });

            var publish2_1 = _sut.PublishAsync(new TestMessage { Value = scope2SentValue }, new PublishOptions { Scope = scope2Name });
            var publish2_2 = _sut.PublishAsync(new TestMessage { Value = scope2SentValue }, new PublishOptions { Scope = scope2Name });

            Task.WaitAll(publish1_1, publish1_2, publish2_1, publish2_2);

            // then
            delivered.Wait(TimeSpan.FromSeconds(10));

            Assert.That(scope1ReceivedValue, Is.EqualTo($"{scope1SentValue}{scope1SentValue}"));
            Assert.That(scope2ReceivedValue, Is.EqualTo($"{scope2SentValue}{scope2SentValue}"));
        }

        [Test]
        public async Task PublishSubscribe_ShouldLimitSimultaneouslyRunningHandlersToOne_IfDefaulSubscriptionOptionsUsed()
        {
            // given
            var delivery = new CountdownEvent(5);

            _sut.Subscribe<TestMessage>(m =>
            {
                Thread.Sleep(TimeSpan.FromSeconds(1));
                delivery.Signal();
            });

            _sut.Start();

            // when
            var sw = new Stopwatch();
            sw.Start();

            for (var i = 0; i < 5; i++)
            {
                await _sut.PublishAsync(new TestMessage());
            }

            delivery.Wait();

            var executioTime = sw.Elapsed;

            // then
            Assert.That(executioTime.Seconds, Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public async Task PublishSubscribe_ShouldLimitSimultaneouslyRunningHandlersToSpecifiedValue_IfMaxConcurrentHandlersSpecified()
        {
            // given
            var delivery = new CountdownEvent(5);

            _sut.Subscribe<TestMessage>(m =>
            {
                Thread.Sleep(TimeSpan.FromSeconds(1));
                delivery.Signal();
            }, new SubscribeOptions { MaxConcurrentHandlers = 5 });

            _sut.Start();

            // when
            var sw = new Stopwatch();
            sw.Start();

            for (var i = 0; i < 10; i++)
            {
                await _sut.PublishAsync(new TestMessage());
            }

            delivery.Wait();

            sw.Stop();
            var executioTime = sw.Elapsed;

            // then
            Assert.That(executioTime.Seconds, Is.GreaterThanOrEqualTo(1));
            Assert.That(executioTime.Seconds, Is.LessThanOrEqualTo(3));
        }
    }
}
