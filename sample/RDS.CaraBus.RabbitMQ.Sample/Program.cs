using System;
using System.Diagnostics.Eventing;

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
                });

                caraBus.Subscribe<Message>(m =>
                {
                    Console.WriteLine($"Subscriber 2 received message: {m.Text}");
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
                var options = new SubscribeOptions {Exclusive = true};

                caraBus.Subscribe<Message>(m =>
                {
                    Console.WriteLine($"Subscriber 1 received message: {m.Text}");
                }, options);

                caraBus.Subscribe<Message>(m =>
                {
                    Console.WriteLine($"Subscriber 2 received message: {m.Text}");
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

    }
}