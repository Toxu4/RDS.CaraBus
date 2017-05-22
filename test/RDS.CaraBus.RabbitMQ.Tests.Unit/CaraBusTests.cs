using System;
using System.Collections.Generic;
using NUnit.Framework;
using RDS.CaraBus.Tests.Unit;
using NSubstitute;
using RabbitMQ.Client;

namespace RDS.CaraBus.RabbitMQ.Tests.Unit
{
    [TestFixture]
    public class CaraBusTests : CaraBusUnitTestsSpec, IDisposable
    {
        private readonly List<IDisposable> _disposables = new List<IDisposable>();

        protected override ICaraBus CreateCaraBus()
        {
            var connectionFactory = Substitute.For<IConnectionFactory>();

            var caraBus = new CaraBus(connectionFactory);

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
