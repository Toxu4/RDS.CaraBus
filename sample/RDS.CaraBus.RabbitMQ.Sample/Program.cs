using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RDS.CaraBus.RabbitMQ.Sample
{
    class Program
    {
        public class Message
        {
            public string Text { get; set; }
        }

        static void Main(string[] args)
        {
            while (true)
            {
                Console.Clear();

                Console.WriteLine("Select sample:");
                Console.WriteLine("1 - Single publisher and single subscriber");
                Console.WriteLine("2 - Single publisher and multiple subscribers");
                Console.WriteLine("3 - Single publisher and multiple subscribers that shares one message queue");
                Console.WriteLine("4 - Scopes");
                Console.WriteLine("empty string - exit");

                Console.Write(">");
                var input = Console.ReadLine();
                if (input == string.Empty)
                {
                    break;
                }

                switch (input)
                {
                    case "1":
                        SinglePublisherSingleSubscriber();
                        break;
                    case "2":
                        SinglePublisherMultipleSubscribers();
                        break;
                    case "3":
                        SinglePublisherMultipleSubscribersThatSharesQueue();
                        break;
                    case "4":
                        Scopes();
                        break;
                }
            }
        }

        private static void SinglePublisherSingleSubscriber()
        {
            Console.Clear();
            Console.WriteLine("Single publisher and single subscriber");
            Console.WriteLine("Enter message text to send or empty string to exit:");

            using (var caraBus = new CaraBus())
            {
                caraBus.Subscribe<Message>(m =>
                {
                    Console.WriteLine($"Received message: {m.Text}");
                    return Task.CompletedTask;
                });

                caraBus.Start();

                while (true)
                {
                    var text = Console.ReadLine();
                    if (text == string.Empty)
                    {
                        break;
                    }

                    caraBus.PublishAsync(new Message { Text = text }).GetAwaiter().GetResult();
                }

                caraBus.Stop();
            }
        }

        private static void SinglePublisherMultipleSubscribers()
        {
            Console.Clear();
            Console.WriteLine("Single publisher and multiple subscribers");
            Console.WriteLine("Enter message text to send or empty string to exit:");

            using (var caraBus = new CaraBus())
            {
                caraBus.Subscribe<Message>(m =>
                {
                    Console.WriteLine($"Subscriber 1 received message: {m.Text}");
                    return Task.CompletedTask;
                });

                caraBus.Subscribe<Message>(m =>
                {
                    Console.WriteLine($"Subscriber 2 received message: {m.Text}");
                    return Task.CompletedTask;
                });

                caraBus.Start();

                while (true)
                {
                    var text = Console.ReadLine();
                    if (text == string.Empty)
                    {
                        break;
                    }

                    caraBus.PublishAsync(new Message { Text = text }).GetAwaiter().GetResult();
                }

                caraBus.Stop();
            }
        }

        private static void SinglePublisherMultipleSubscribersThatSharesQueue()
        {
            Console.Clear();
            Console.WriteLine("Single publisher and multiple subscribers that shares one message queue");
            Console.WriteLine("Enter message text to send or empty string to exit:");

            using (var caraBus = new CaraBus())
            {
                var options = new SubscribeOptions { Exclusive = true };

                caraBus.Subscribe<Message>(m =>
                {
                    Console.WriteLine($"Subscriber 1 received message: {m.Text}");
                    return Task.CompletedTask;
                }, options);

                caraBus.Subscribe<Message>(m =>
                {
                    Console.WriteLine($"Subscriber 2 received message: {m.Text}");
                    return Task.CompletedTask;
                }, options);

                caraBus.Start();

                while (true)
                {
                    var text = Console.ReadLine();
                    if (text == string.Empty)
                    {
                        break;
                    }

                    caraBus.PublishAsync(new Message { Text = text }).GetAwaiter().GetResult();
                }

                caraBus.Stop();
            }
        }

        private static void Scopes()
        {
            Console.Clear();
            Console.WriteLine("Scopes");
            Console.WriteLine("There are two scopes declared for this sample: 'one' and 'two'");
            Console.WriteLine("Enter '[scope]<space>message text' to send or empty string to exit:");

            using (var caraBus = new CaraBus())
            {
                caraBus.Subscribe<Message>(m =>
                {
                    Console.WriteLine($"[scope 'one'] subscriber 1: received message: {m.Text}");
                    return Task.CompletedTask;
                }, new SubscribeOptions { Scope = "one", Exclusive = true });

                caraBus.Subscribe<Message>(m =>
                {
                    Console.WriteLine($"[scope 'one'] subscriber 2: received message: {m.Text}");
                    return Task.CompletedTask;
                }, new SubscribeOptions { Scope = "one", Exclusive = true });

                caraBus.Subscribe<Message>(m =>
                {
                    Console.WriteLine($"[scope 'two'] subscriber 1: received message: {m.Text}");
                    return Task.CompletedTask;
                }, new SubscribeOptions { Scope = "two" });

                caraBus.Subscribe<Message>(m =>
                {
                    Console.WriteLine($"[scope 'two'] subscriber 2: received message: {m.Text}");
                    return Task.CompletedTask;
                }, new SubscribeOptions { Scope = "two" });

                caraBus.Start();


                var scopeOneMessage = "Hello for scope 'one'";
                WriteDemoText($"one {scopeOneMessage}");
                caraBus.PublishAsync(new Message { Text = scopeOneMessage }, new PublishOptions { Scope = "one" }).GetAwaiter().GetResult();
                Thread.Sleep(TimeSpan.FromSeconds(1));

                var scopeTwoMessage = "Hello for scope 'two'";
                WriteDemoText($"two {scopeTwoMessage}");
                caraBus.PublishAsync(new Message { Text = scopeTwoMessage }, new PublishOptions { Scope = "two" }).GetAwaiter().GetResult();

                while (true)
                {
                    var text = Console.ReadLine();
                    if (text == string.Empty)
                    {
                        break;
                    }

                    var parts = text.Split(' ');

                    var scope = parts[0];
                    text = parts.Length > 1
                        ? parts.Skip(1).Aggregate((c, n) => c + " " + n)
                        : String.Empty;

                    caraBus.PublishAsync(new Message { Text = text }, new PublishOptions { Scope = scope }).GetAwaiter().GetResult();
                }

                caraBus.Stop();
            }
        }

        private static void WriteDemoText(string text)
        {
            foreach (var c in text)
            {
                Console.Write(c);
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
            }
            Console.WriteLine("<enter>");
        }

    }
}