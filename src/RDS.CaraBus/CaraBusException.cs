using System;

namespace RDS.CaraBus
{
    public class CaraBusException : Exception
    {
        public CaraBusException(string message, Exception innerException = null) : base(message, innerException)
        {

        }
    }
}
