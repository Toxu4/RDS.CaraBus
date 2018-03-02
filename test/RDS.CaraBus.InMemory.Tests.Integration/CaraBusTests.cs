using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using RDS.CaraBus.Tests.Integration;

namespace RDS.CaraBus.InMemory.Tests.Integration
{
    [TestFixture]
    public class CaraBusTests : CaraBusIntegrationTestsSpec
    {
        public CaraBusTests()
        {
            ThreadPool.SetMaxThreads(50, 50);
            ThreadPool.SetMinThreads(50, 50);
        }

        protected override ICaraBus CreateCaraBus(CaraBusBaseOptions caraBusBaseOptions)
        {
            return new InMemoryCaraBus(new InMemoryCaraBusOptions
            {
                MaxDegreeOfParallelism = caraBusBaseOptions.MaxDegreeOfParallelism,
                AutoStart = caraBusBaseOptions.AutoStart,
                TimeoutOnStop = caraBusBaseOptions.TimeoutOnStop
            });
        }
    }
}
