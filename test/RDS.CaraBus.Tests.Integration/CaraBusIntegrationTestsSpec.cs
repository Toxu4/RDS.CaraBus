using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
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
        public async Task TearDown()
        {
            if (_sut.IsRunning())
            {
                await _sut.StopAsync();
            }
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

            await _sut.StartAsync();

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

            await _sut.StartAsync();

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

            await _sut.StartAsync();

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

            await _sut.StartAsync();

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

            var options = SubscribeOptions.Exclusive();

            _sut.Subscribe<TestMessage>(m =>
            {                
                Interlocked.Increment(ref deliveryCount);
            }, options);

            _sut.Subscribe<TestMessage>(m =>
            {
                Interlocked.Increment(ref deliveryCount);
            }, options);

            await _sut.StartAsync();

            // when
            await _sut.PublishAsync(new TestMessage { Value = sentValue });

            // then
            Thread.Sleep(TimeSpan.FromSeconds(2));

            Assert.That(deliveryCount, Is.EqualTo(1));
        }

        [Test]
        public async Task PublishSubscribe_ShouldDeliverMessagesInsideScopeOnly()
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
            }, SubscribeOptions.NonExclusive(opt => opt.Scope = scope1Name));

            _sut.Subscribe<TestMessage>(m =>
            {
                scope2ReceivedValue = $"{scope2ReceivedValue}{m.Value}";
                delivered.Signal();
            }, SubscribeOptions.NonExclusive(opt => opt.Scope = scope2Name));

            await _sut.StartAsync();

            // when
            var publish1_1 = _sut.PublishAsync(new TestMessage { Value = scope1SentValue }, new PublishOptions { Scope = scope1Name });
            var publish1_2 = _sut.PublishAsync(new TestMessage { Value = scope1SentValue }, new PublishOptions { Scope = scope1Name });

            var publish2_1 = _sut.PublishAsync(new TestMessage { Value = scope2SentValue }, new PublishOptions { Scope = scope2Name });
            var publish2_2 = _sut.PublishAsync(new TestMessage { Value = scope2SentValue }, new PublishOptions { Scope = scope2Name });

            Task.WaitAll(publish1_1, publish1_2, publish2_1, publish2_2);

            // then
            delivered.Wait(TimeSpan.FromSeconds(2));

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

            await _sut.StartAsync();

            // when
            var sw = new Stopwatch();
            sw.Start();

            for (var i = 0; i < 5; i++)
            {
                await _sut.PublishAsync(new TestMessage());
            }

            delivery.Wait(TimeSpan.FromSeconds(10));

            var executioTime = sw.Elapsed;

            // then
            Assert.That(delivery.CurrentCount, Is.EqualTo(0));
            Assert.That(executioTime.Seconds, Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public async Task PublishSubscribe_ShouldLimitSimultaneouslyRunningHandlersToSpecifiedValue_IfMaxConcurrentHandlersSpecified()
        {
            // given
            var delivery = new CountdownEvent(10);

            _sut.Subscribe<TestMessage>(m =>
            {
                Thread.Sleep(TimeSpan.FromSeconds(1));
                delivery.Signal();
            }, SubscribeOptions.NonExclusive(opt => opt.MaxConcurrentHandlers = 5));

            await _sut.StartAsync();

            // when
            var sw = new Stopwatch();
            sw.Start();

            for (var i = 0; i < 10; i++)
            {
                await _sut.PublishAsync(new TestMessage());
            }

            delivery.Wait(TimeSpan.FromSeconds(15));

            sw.Stop();
            var executioTime = sw.Elapsed;

            // then
            Assert.That(executioTime.Seconds, Is.GreaterThanOrEqualTo(1));
            Assert.That(executioTime.Seconds, Is.LessThanOrEqualTo(3));
        }

        [Test]
        public async Task PublishSubscribe_WhenHandlerReturnTask_ShouldWaitTillHandlerEnd()
        {
            // given
            var delivery = new CountdownEvent(2);

            var mockClass = Substitute.For<MockClass>();

            var startValue = "start";
            var endValue = "end";

            _sut.Subscribe<TestMessage>(async m =>
            {
                mockClass.Test(startValue);
                await Task.Delay(TimeSpan.FromSeconds(1));
                mockClass.Test(endValue);
                delivery.Signal();
            }, SubscribeOptions.NonExclusive(opt => opt.MaxConcurrentHandlers = 1));

            await _sut.StartAsync();

            // when
            await _sut.PublishAsync(new TestMessage());
            await _sut.PublishAsync(new TestMessage());

            delivery.Wait(TimeSpan.FromSeconds(15));

            // then
            Received.InOrder(() =>
            {
                // Проверяем, что перед началом обработки следующего сообщения заканчивается обработка первого
                mockClass.Test(startValue);
                mockClass.Test(endValue);

                mockClass.Test(startValue);
                mockClass.Test(endValue);
            });
        }

        [Test]
        [Ignore("I dont know how create task without running with true async features")]
        public async Task Stop_WhenSubscriberRunning_ShouldWaitTillHandlerEnd()
        {
            // given
            var notEndingHandlers = 0;

            _sut.Subscribe<TestMessage>(async m =>
            {
                notEndingHandlers++;
                await Task.Delay(TimeSpan.FromSeconds(1));
                notEndingHandlers--;
            });

            await _sut.StartAsync();

            // when
            await _sut.PublishAsync(new TestMessage());

            // then
            await _sut.StopAsync();
            Assert.AreEqual(0, notEndingHandlers);
        }

        [Test]
        public async Task PublishSubscribe_WhenGeneric_ShouldDeliverMessage()
        {
            // given           
            var sentValue = Guid.NewGuid().ToString();
            var receivedValue = string.Empty;

            var delivered = new ManualResetEvent(false);

            _sut.Subscribe<TestGenericMessage<string>>(m =>
            {
                receivedValue = m.Value;
                delivered.Set();
            });

            await _sut.StartAsync();

            // when
            await _sut.PublishAsync(new TestGenericMessage<string> { Value = sentValue });

            // then
            delivered.WaitOne(TimeSpan.FromSeconds(2));

            Assert.That(receivedValue, Is.EqualTo(sentValue));
        }

        [Test]
        public async Task PublishSubscribe_WhenComplexGeneric_ShouldDeliverMessage()
        {
            // given           
            var sentValue = Guid.NewGuid().ToString();
            string receivedValue = null;

            var delivered = new ManualResetEvent(false);

            _sut.Subscribe<TestGenericMessage<TestComplexMessage>>(m =>
            {
                receivedValue = m.Value.Value1;
                delivered.Set();
            });

            await _sut.StartAsync();

            // when
            await _sut.PublishAsync(new TestGenericMessage<TestComplexMessage> {Value = new TestComplexMessage(sentValue) });

            // then
            delivered.WaitOne(TimeSpan.FromSeconds(2));

            Assert.That(receivedValue, Is.EqualTo(sentValue));
        }

        [Test]
        public async Task PublishSubscribe_ShouldDeliverMessagesEveryGroup()
        {
            // given
            var delivered = new CountdownEvent(16);

            var deliveryCountGroup1_sub1 = 0;
            var deliveryCountGroup1_sub2 = 0;

            var deliveryCountGroup2_sub1 = 0;
            var deliveryCountGroup2_sub2 = 0;

            var deliveryCountGroup3_sub1 = 0;
            var deliveryCountGroup3_sub2 = 0;
            var deliveryCountGroup3_sub3 = 0;
            var deliveryCountGroup3_sub4 = 0;

            var deliveryCountNoGroup = 0;

            _sut.Subscribe<TestMessage>(m =>
            {
                Interlocked.Increment(ref deliveryCountGroup1_sub1);
                delivered.Signal();
            }, SubscribeOptions.Exclusive("GROUP 1"));

            _sut.Subscribe<TestMessage>(m =>
            {
                Interlocked.Increment(ref deliveryCountGroup1_sub2);
                delivered.Signal();
            }, SubscribeOptions.Exclusive("GROUP 1"));

            _sut.Subscribe<TestMessage>(m =>
            {
                Interlocked.Increment(ref deliveryCountGroup2_sub1);
                delivered.Signal();
            }, SubscribeOptions.Exclusive("GROUP 2"));

            _sut.Subscribe<TestMessage>(m =>
            {
                Interlocked.Increment(ref deliveryCountGroup2_sub2);
                delivered.Signal();
            }, SubscribeOptions.Exclusive("GROUP 2"));

            _sut.Subscribe<TestMessage>(m =>
            {
                Interlocked.Increment(ref deliveryCountGroup3_sub1);
                delivered.Signal();
            }, SubscribeOptions.Exclusive("GROUP 3"));

            _sut.Subscribe<TestMessage>(m =>
            {
                Interlocked.Increment(ref deliveryCountGroup3_sub2);
                delivered.Signal();
            }, SubscribeOptions.Exclusive("GROUP 3"));
            _sut.Subscribe<TestMessage>(m =>
            {
                Interlocked.Increment(ref deliveryCountGroup3_sub3);
                delivered.Signal();
            }, SubscribeOptions.Exclusive("GROUP 3"));
            _sut.Subscribe<TestMessage>(m =>
            {
                Interlocked.Increment(ref deliveryCountGroup3_sub4);
                delivered.Signal();
            }, SubscribeOptions.Exclusive("GROUP 3"));


            _sut.Subscribe<TestMessage>(m =>
            {
                Interlocked.Increment(ref deliveryCountNoGroup);
                delivered.Signal();
            }, SubscribeOptions.Exclusive());


            await _sut.StartAsync();

            // when
            var publish1_1 = _sut.PublishAsync(new TestMessage());
            var publish1_2 = _sut.PublishAsync(new TestMessage());

            var publish2_1 = _sut.PublishAsync(new TestMessage());
            var publish2_2 = _sut.PublishAsync(new TestMessage());

            Task.WaitAll(publish1_1, publish1_2, publish2_1, publish2_2);

            // then
            delivered.Wait(TimeSpan.FromSeconds(2));

            Assert.That(deliveryCountGroup1_sub1, Is.EqualTo(2));
            Assert.That(deliveryCountGroup1_sub2, Is.EqualTo(2));
            Assert.That(deliveryCountGroup2_sub1, Is.EqualTo(2));
            Assert.That(deliveryCountGroup2_sub2, Is.EqualTo(2));

            Assert.That(deliveryCountGroup3_sub1, Is.EqualTo(1));
            Assert.That(deliveryCountGroup3_sub2, Is.EqualTo(1));
            Assert.That(deliveryCountGroup3_sub3, Is.EqualTo(1));
            Assert.That(deliveryCountGroup3_sub4, Is.EqualTo(1));

            Assert.That(deliveryCountNoGroup, Is.EqualTo(4));
        }

        public class MockClass
        {
            public virtual void Test(string someValue)
            {

            }
        }

        private class TestGenericMessage<T>
        {
            public TestGenericMessage()
            {
                
            }

            public TestGenericMessage(T value)
            {
                Value = value;
            }

            public T Value { get; set; }
        }

        private class TestComplexMessage
        {
            public TestComplexMessage()
            {
                
            }

            public TestComplexMessage(string value1)
            {
                Value1 = value1;
            }

            public string Value1 { get; set; }
        }
    }
}
