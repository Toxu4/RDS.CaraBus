using System;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using RDS.CaraBus.RabbitMQ;

namespace RDS.CaraBus.Tests.Unit
{
    public abstract class CaraBusUnitTestsSpec
    {
        protected abstract ICaraBus CreateCaraBus(CaraBusBaseOptions caraBusBaseOptions);

        [Test]
        public async Task Start_ShouldThrowException_IfAlreadyStarted()
        {
            // given
            var caraBus = CreateCaraBus(new CaraBusBaseOptions());
            await caraBus.StartAsync();

            // when
            Func<Task> action = async () => await caraBus.StartAsync(); 

            // then
            action.Should().Throw<CaraBusException>().WithMessage("Already running");
        }

        [Test]
        public void Publish_ShouldThrowException_IfNotRunning()
        {
            // given
            var caraBus = CreateCaraBus(new CaraBusBaseOptions());

            // when
            Action action = () => caraBus.PublishAsync(new { }).GetAwaiter().GetResult();

            // then
            action.Should().Throw<CaraBusException>().WithMessage("Should be running");
        }

        [Test]
        public async Task Subscribe_ShouldThrowException_IfRunning()
        {
            // given
            var caraBus = CreateCaraBus(new CaraBusBaseOptions());
            await caraBus.StartAsync();

            // when
            Action action = () => caraBus.SubscribeAsync<Object>((o, token) => Task.CompletedTask);

            // then
            action.Should().Throw<CaraBusException>().WithMessage("Should be stopped");
        }

    }
}