using System;
using System.Collections.Generic;
using NUnit.Framework;
using RDS.CaraBus.Tests.Unit;

namespace RDS.CaraBus.RabbitMQ.Tests.Unit
{
    [TestFixture]
    public class CaraBusTests : CaraBusUnitTestsSpec, IDisposable
    {
        private readonly List<IDisposable> _disposables = new List<IDisposable>();

        protected override ICaraBus CreateCaraBus(CaraBusBaseOptions caraBusBaseOptions)
        {
            var caraBus = new RabbitMQCaraBus(new RabbitMQCaraBusOptions
            {
                ConnectionString = "amqp://localhost",
                MaxDegreeOfParallelism = caraBusBaseOptions.MaxDegreeOfParallelism,
                AutoStart = caraBusBaseOptions.AutoStart,
                TimeoutOnStop = caraBusBaseOptions.TimeoutOnStop
            });

            _disposables.Add(caraBus);

            return caraBus;
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

            foreach (var disposable in _disposables)
            {
                disposable.Dispose();
            }
        }
    }
}
