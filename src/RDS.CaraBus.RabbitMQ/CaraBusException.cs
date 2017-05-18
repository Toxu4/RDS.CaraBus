using System;

namespace RDS.CaraBus.RabbitMQ
{
    public class CaraBusException : Exception
    {
        public CaraBusException(string alreadyRunning, Exception innerException = null)
            :base(alreadyRunning, innerException)
        {
            
        }
    }
}
