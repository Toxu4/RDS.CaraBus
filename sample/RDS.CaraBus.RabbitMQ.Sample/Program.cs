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

        static async Task Main(string[] args)
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
                        await SinglePublisherSingleSubscriber();
                        break;
                    case "2":
                        await SinglePublisherMultipleSubscribers();
                        break;
                    case "3":
                        await SinglePublisherMultipleSubscribersThatSharesQueue();
                        break;
                    case "4":
                        await Scopes();
                        break;
                }
            }
        }

        private static async Task SinglePublisherSingleSubscriber()
        {
            Console.Clear();
            Console.WriteLine("Single publisher and single subscriber");
            Console.WriteLine("Enter message text to send or empty string to exit:");

            using (var caraBus = new CaraBus())
            {
                caraBus.Subscribe<Message>(m =>
                {
                    Console.WriteLine($"Received message: {m.Text}");
                });

                await caraBus.StartAsync();

                while (true)
                {
                    var text = Console.ReadLine();
                    if (text == string.Empty)
                    {
                        break;
                    }

                    await caraBus.PublishAsync(new Message { Text = text });
                }

                await caraBus.StopAsync();
            }
        }

        private static async Task SinglePublisherMultipleSubscribers()
        {
            Console.Clear();
            Console.WriteLine("Single publisher and multiple subscribers");
            Console.WriteLine("Enter message text to send or empty string to exit:");

            using (var caraBus = new CaraBus())
            {
                caraBus.Subscribe<Message>(m =>
                {
                    Console.WriteLine($"Subscriber 1 received message: {m.Text}");
                });

                caraBus.Subscribe<Message>(m =>
                {
                    Console.WriteLine($"Subscriber 2 received message: {m.Text}");
                });

                await caraBus.StartAsync();

                while (true)
                {
                    var text = Console.ReadLine();
                    if (text == string.Empty)
                    {
                        break;
                    }

                    await caraBus.PublishAsync(new Message { Text = text });
                }

                await caraBus.StopAsync();
            }
        }

        private static async Task SinglePublisherMultipleSubscribersThatSharesQueue()
        {
            Console.Clear();
            Console.WriteLine("Single publisher and multiple subscribers that shares one message queue");
            Console.WriteLine("Enter message text to send or empty string to exit:");

            using (var caraBus = new CaraBus())
            {
                var options = SubscribeOptions.Exclusive();

                caraBus.Subscribe<Message>(m =>
                {
                    Console.WriteLine($"Subscriber 1 received message: {m.Text}");
                }, options);

                caraBus.Subscribe<Message>(m =>
                {
                    Console.WriteLine($"Subscriber 2 received message: {m.Text}");
                }, options);

                await caraBus.StartAsync();

                while (true)
                {
                    var text = Console.ReadLine();
                    if (text == string.Empty)
                    {
                        break;
                    }

                    await caraBus.PublishAsync(new Message { Text = text });
                }

                await caraBus.StopAsync();
            }
        }

        private static async Task Scopes()
        {
            Console.Clear();
            Console.WriteLine("Scopes");
            Console.WriteLine("There are two scopes declared for this sample: 'one' and 'two'");
            Console.WriteLine("Enter '[scope]<space>message text' to send or empty string to exit:");

            using (var caraBus = new CaraBus())
            {
                caraBus.Subscribe<Message>(m =>
                {
                    Console.WriteLine($"[scope 'one'], default group subscriber 1: received message: {m.Text}");
                }, SubscribeOptions.Exclusive(opt => opt.Scope = "one"));

                caraBus.Subscribe<Message>(m =>
                {
                    Console.WriteLine($"[scope 'one'], default group subscriber 2: received message: {m.Text}");
                }, SubscribeOptions.Exclusive(opt => opt.Scope = "one"));

                caraBus.Subscribe<Message>(m =>
                {
                    Console.WriteLine($"[scope 'one'], custom some group, subscriber 1: received message: {m.Text}");
                }, SubscribeOptions.Exclusive("some group", opt => opt.Scope = "one"));

                caraBus.Subscribe<Message>(m =>
                {
                    Console.WriteLine($"[scope 'one'] custom some group, subscriber 2: received message: {m.Text}");
                }, SubscribeOptions.Exclusive("some group", opt => opt.Scope = "one"));

                caraBus.Subscribe<Message>(m =>
                {
                    Console.WriteLine($"[scope 'two'] subscriber 1: received message: {m.Text}");
                }, SubscribeOptions.NonExclusive(opt => opt.Scope = "two"));

                caraBus.Subscribe<Message>(m =>
                {
                    Console.WriteLine($"[scope 'two'] subscriber 2: received message: {m.Text}");
                }, SubscribeOptions.NonExclusive(opt => opt.Scope = "two"));

                await caraBus.StartAsync();

                var scopeOneMessage = "Hello for scope 'one'";
                WriteDemoText($"one {scopeOneMessage}");
                await caraBus.PublishAsync(new Message { Text = scopeOneMessage }, new PublishOptions { Scope = "one" });

                await Task.Delay(TimeSpan.FromSeconds(2));

                var scopeTwoMessage = "Hello for scope 'two'";
                WriteDemoText($"two {scopeTwoMessage}");
                await caraBus.PublishAsync(new Message { Text = scopeTwoMessage }, new PublishOptions { Scope = "two" });

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

                    await caraBus.PublishAsync(new Message { Text = text }, new PublishOptions { Scope = scope });
                }

                await caraBus.StopAsync();
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