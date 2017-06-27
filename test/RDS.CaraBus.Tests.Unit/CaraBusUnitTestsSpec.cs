using System;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using RDS.CaraBus.RabbitMQ;

namespace RDS.CaraBus.Tests.Unit
{
    public abstract class CaraBusUnitTestsSpec
    {
        protected abstract ICaraBus CreateCaraBus();

        [Test]
        public void Ctor_ShouldCreateInNotRunningState()
        {
            // given

            // when
            var caraBus = CreateCaraBus();

            // then
            Assert.That(caraBus.IsRunning(), Is.False);
        }

        [Test]
        public async Task Start_ShouldMakeCaraBusRunning()
        {
            // given
            var caraBus = CreateCaraBus();

            // when
            await caraBus.StartAsync();

            // then
            Assert.That(caraBus.IsRunning(), Is.True);
        }

        [Test]
        public async Task Start_ShouldThrowException_IfAlreadyStarted()
        {
            // given
            var caraBus = CreateCaraBus();
            await caraBus.StartAsync();

            // when
            Func<Task> action = async () => await caraBus.StartAsync(); 

            // then
            action.ShouldThrow<CaraBusException>().WithMessage("Already running");
        }

        [Test]
        public async Task Stop_ShouldMakeCaraBusStopped()
        {
            // given
            var caraBus = CreateCaraBus();
            await caraBus.StartAsync();

            // when
            await caraBus.StopAsync();

            // then
            Assert.That(caraBus.IsRunning(), Is.False);
        }

        [Test]
        public void Stop_ShouldThrowException_IfAlreadyStopped()
        {
            // given
            var caraBus = CreateCaraBus();

            // when
            Func<Task> action = async () => await caraBus.StopAsync();

            // then
            action.ShouldThrow<CaraBusException>().WithMessage("Already stopped");
        }

        [Test]
        public void Publish_ShouldThrowException_IfNotRunning()
        {
            // given
            var caraBus = CreateCaraBus();

            // when
            Action action = () => caraBus.PublishAsync(new { }).GetAwaiter().GetResult();

            // then
            action.ShouldThrow<CaraBusException>().WithMessage("Should be running");
        }

        [Test]
        public async Task Subscribe_ShouldThrowException_IfRunning()
        {
            // given
            var caraBus = CreateCaraBus();
            await caraBus.StartAsync();

            // when
            Action action = () => caraBus.Subscribe<Object>(o => Task.CompletedTask);

            // then
            action.ShouldThrow<CaraBusException>().WithMessage("Should be stopped");
        }

    }
}