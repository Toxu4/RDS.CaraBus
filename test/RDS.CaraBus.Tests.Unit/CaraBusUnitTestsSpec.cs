using System;
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
        public void Start_ShouldMakeCaraBusRunning()
        {
            // given
            var caraBus = CreateCaraBus();

            // when
            caraBus.Start();

            // then
            Assert.That(caraBus.IsRunning(), Is.True);
        }

        [Test]
        public void Start_ShouldThrowException_IfAlreadyStarted()
        {
            // given
            var caraBus = CreateCaraBus();
            caraBus.Start();

            // when
            Action action = () => caraBus.Start();


            // then
            action.ShouldThrow<CaraBusException>().WithMessage("Already running");
        }

        [Test]
        public void Stop_ShouldMakeCaraBusStopped()
        {
            // given
            var caraBus = CreateCaraBus();
            caraBus.Start();

            // when
            caraBus.Stop();

            // then
            Assert.That(caraBus.IsRunning(), Is.False);
        }

        [Test]
        public void Stop_ShouldThrowException_IfAlreadyStopped()
        {
            // given
            var caraBus = CreateCaraBus();

            // when
            Action action = () => caraBus.Stop();

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
        public void Subscribe_ShouldThrowException_IfRunning()
        {
            // given
            var caraBus = CreateCaraBus();
            caraBus.Start();

            // when
            Action action = () => caraBus.Subscribe<Object>(o => { });

            // then
            action.ShouldThrow<CaraBusException>().WithMessage("Should be stopped");
        }

    }
}
