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
                Console.WriteLine("Select sample:");
                Console.WriteLine("1 - Single publisher and single subscription");
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
                }

                Console.Clear();
            }
        }

        private static void SinglePublisherSingleSubscriber()
        {
            Console.WriteLine("Single publisher and single subscription");
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
    }
}