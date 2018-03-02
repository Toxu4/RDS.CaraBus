using System;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RDS.CaraBus.Utility;

namespace RDS.CaraBus.Tests
{
    [TestFixture]
    public class TaskQueueTests
    {
        [Test]
        public async Task CanProcessEmptyQueue()
        {
            var queue = new TaskQueue();
            await Task.Delay(30);
            queue.Dispose();
            await Task.Delay(1);
            Assert.AreEqual(0, queue.Working);
        }

        [Test]
        public void CanRun()
        {
            int completed = 0;
            var countdown = new CountdownEvent(1);
            using (var queue = new TaskQueue(autoStart: false))
            {
                queue.WaitWhenEmpty.ContinueWith(t => countdown.Signal());

                queue.Enqueue(() => {
                    Interlocked.Increment(ref completed);
                    return Task.CompletedTask;
                });

                Assert.AreEqual(1, queue.Queued);
                queue.Start();

                countdown.Wait(TimeSpan.FromSeconds(2));
                Assert.AreEqual(0, countdown.CurrentCount);
                Assert.AreEqual(0, queue.Queued);
                Assert.AreEqual(1, completed);
            }
        }

        [Test]
        public void CanRunAndWait()
        {
            int completed = 0;
            var countdown = new CountdownEvent(1);
            using (var queue = new TaskQueue(autoStart: false))
            {
                void Sub(int id)
                {
                    queue.WaitWhenEmpty.ContinueWith(t =>
                    {
                        if (queue.WaitWhenEmpty.Id != id)
                        {
                            Sub(queue.WaitWhenEmpty.Id);
                        }

                        return countdown.Signal();
                    });
                }

                Sub(queue.WaitWhenEmpty.Id);

                queue.Enqueue(() => {
                    Interlocked.Increment(ref completed);
                    return Task.CompletedTask;
                });

                Assert.AreEqual(1, queue.Queued);
                queue.Start();

                countdown.Wait(TimeSpan.FromSeconds(2));
                Assert.AreEqual(0, countdown.CurrentCount);
                Assert.AreEqual(0, queue.Queued);
                Assert.AreEqual(1, completed);

                Thread.Sleep(30);
                countdown.Reset();

                queue.Enqueue(() => {
                    Interlocked.Increment(ref completed);
                    return Task.CompletedTask;
                });

                countdown.Wait(TimeSpan.FromSeconds(2));
                Assert.AreEqual(0, countdown.CurrentCount);
                Assert.AreEqual(0, queue.Queued);
                Assert.AreEqual(2, completed);
            }
        }

        [Test]
        public void CanRespectMaxItems()
        {
            using (var queue = new TaskQueue(maxItems: 1, autoStart: false))
            {
                queue.Enqueue(() => Task.CompletedTask);
                queue.Enqueue(() => Task.CompletedTask);

                Assert.AreEqual(1, queue.Queued);
                Assert.AreEqual(0, queue.Working);
            }
        }

        [Test]
        public void CanHandleTaskFailure()
        {
            int completed = 0;
            var countdown = new CountdownEvent(1);
            using (var queue = new TaskQueue(autoStart: false))
            {
                queue.WaitWhenEmpty.ContinueWith(t =>
                {
                    if (completed > 0)
                        countdown.Signal();
                });


                queue.Enqueue(() => {
                    throw new Exception("Exception in Queued Task");
                });

                queue.Enqueue(async () => {
                    throw new Exception("Exception in Queued Task");
                });

                queue.Enqueue(() => {
                    Interlocked.Increment(ref completed);
                    return Task.CompletedTask;
                });

                Assert.AreEqual(3, queue.Queued);
                Assert.AreEqual(0, queue.Working);
                queue.Start();

                countdown.Wait(TimeSpan.FromSeconds(2));
                Assert.AreEqual(0, countdown.CurrentCount);
                Assert.AreEqual(0, queue.Queued);
                Assert.AreEqual(0, queue.Working);
                Assert.AreEqual(1, completed);
            }
        }

        [Test]
        public void CanRunInParallel()
        {
            var countdown = new CountdownEvent(1);
            using (var queue = new TaskQueue(maxDegreeOfParallelism: 2, autoStart: false))
            {
                queue.WaitWhenEmpty.ContinueWith(t => countdown.Signal());

                int completed = 0;
                queue.Enqueue(async () => { await Task.Delay(10); Interlocked.Increment(ref completed); });
                queue.Enqueue(async () => { await Task.Delay(50); Interlocked.Increment(ref completed); });
                queue.Enqueue(() => { Assert.AreEqual(2, queue.Working); Interlocked.Increment(ref completed); return Task.CompletedTask; });
                Assert.AreEqual(3, queue.Queued);
                Assert.AreEqual(0, queue.Working);

                queue.Start();

                countdown.Wait(TimeSpan.FromSeconds(2));
                Assert.AreEqual(0, countdown.CurrentCount);
                Assert.AreEqual(0, queue.Queued);
                Assert.AreEqual(0, queue.Working);
                Assert.AreEqual(3, completed);
            }
        }

        [Test]
        public void CanRunContinuously()
        {
            int completed = 0;
            var countdown = new CountdownEvent(1);
            using (var queue = new TaskQueue())
            {
                queue.WaitWhenEmpty.ContinueWith(t =>
                {
                    if (completed > 2)
                        countdown.Signal();
                });

                queue.Enqueue(() => { Interlocked.Increment(ref completed); return Task.CompletedTask; });
                queue.Enqueue(() => { Interlocked.Increment(ref completed); return Task.CompletedTask; });
                queue.Enqueue(() => { Interlocked.Increment(ref completed); return Task.CompletedTask; });
                //Assert.InRange(queue.Queued, 1, 3);

                countdown.Wait(TimeSpan.FromSeconds(2));
                Assert.AreEqual(0, countdown.CurrentCount);
                Assert.AreEqual(0, queue.Queued);
                Assert.AreEqual(0, queue.Working);
                Assert.AreEqual(3, completed);
            }
        }

        [Test]
        public void CanProcessQuickly()
        {
            const int NumberOfEnqueuedItems = 1000;

            int completed = 0;
            var countdown = new CountdownEvent(1);
            using (var queue = new TaskQueue(autoStart: false))
            {
                queue.WaitWhenEmpty.ContinueWith(t => countdown.Signal());

                for (int i = 0; i < NumberOfEnqueuedItems; i++)
                {
                    queue.Enqueue(() => {
                        Interlocked.Increment(ref completed);
                        return Task.CompletedTask;
                    });
                }

                Assert.AreEqual(NumberOfEnqueuedItems, queue.Queued);
                var sw = Stopwatch.StartNew();
                queue.Start();

                countdown.Wait(TimeSpan.FromSeconds(5));
                Assert.AreEqual(0, countdown.CurrentCount);
                Assert.AreEqual(0, queue.Queued);
                Assert.AreEqual(0, queue.Working);
                Assert.AreEqual(NumberOfEnqueuedItems, completed);
            }
        }

        [Test]
        public void CanProcessInParrallelQuickly()
        {
            const int NumberOfEnqueuedItems = 1000;
            const int MaxDegreeOfParallelism = 2;

            int completed = 0;
            var countdown = new CountdownEvent(1);
            using (var queue = new TaskQueue(maxDegreeOfParallelism: MaxDegreeOfParallelism, autoStart: false))
            {
                queue.WaitWhenEmpty.ContinueWith(t => countdown.Signal());

                for (int i = 0; i < NumberOfEnqueuedItems; i++)
                {
                    queue.Enqueue(() => {
                        Interlocked.Increment(ref completed);
                        return Task.CompletedTask;
                    });
                }

                Assert.AreEqual(NumberOfEnqueuedItems, queue.Queued);

                var sw = Stopwatch.StartNew();
                queue.Start();

                countdown.Wait(TimeSpan.FromSeconds(5));
                //Assert.InRange(countdown.CurrentCount, -1, 0); // TODO: There is a possibility where on completed could be called twice.
                Assert.AreEqual(0, queue.Queued);
                Assert.AreEqual(0, queue.Working);
                Assert.AreEqual(NumberOfEnqueuedItems, completed);
            }
        }

        [Test]
        public void CanProcessInParrallelQuicklyWithRandomDelays()
        {
            const int NumberOfEnqueuedItems = 500;
            const int MaxDelayInMilliseconds = 10;
            const int MaxDegreeOfParallelism = 4;

            int completed = 0;
            var countdown = new CountdownEvent(1);
            using (var queue = new TaskQueue(maxDegreeOfParallelism: MaxDegreeOfParallelism, autoStart: false))
            {
                queue.WaitWhenEmpty.ContinueWith(t => countdown.Signal());

                for (int i = 0; i < NumberOfEnqueuedItems; i++)
                {
                    queue.Enqueue(async () => {
                        var delay = TimeSpan.FromMilliseconds(new Random().Next(0, MaxDelayInMilliseconds));
                        await Task.Delay(delay);
                        Interlocked.Increment(ref completed);
                    });
                }

                Assert.AreEqual(NumberOfEnqueuedItems, queue.Queued);

                var sw = Stopwatch.StartNew();
                queue.Start();

                countdown.Wait(TimeSpan.FromSeconds(NumberOfEnqueuedItems * MaxDelayInMilliseconds));
                Assert.AreEqual(0, countdown.CurrentCount);
                Assert.AreEqual(0, queue.Queued);
                Assert.AreEqual(0, queue.Working);
                Assert.AreEqual(NumberOfEnqueuedItems, completed);
            }
        }

        //[Test]
        //public void Benchmark()
        //{
        //    var summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<TaskQueueBenchmark>();
        //}
    }

    //[MemoryDiagnoser]
    //[ShortRunJob]
    //public class TaskQueueBenchmark
    //{
    //    [Params(1, 2, 4)]
    //    public byte MaxDegreeOfParallelism { get; set; }

    //    private readonly CountdownEvent _countdown = new CountdownEvent(1);

    //    [Benchmark]
    //    public void Run()
    //    {
    //        _countdown.Reset();
    //        using (var queue = new TaskQueue(autoStart: false, maxDegreeOfParallelism: MaxDegreeOfParallelism))
    //        {
    //            queue.WaitWhenEmpty.ContinueWith(t => _countdown.Signal());

    //            for (int i = 0; i < 100; i++)
    //                queue.Enqueue(() => Task.CompletedTask);

    //            queue.Start();
    //            _countdown.Wait(5000);
    //        }
    //    }
    //}
}