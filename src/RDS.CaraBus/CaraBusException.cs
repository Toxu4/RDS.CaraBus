using System;

namespace RDS.CaraBus
{
    public class CaraBusException : Exception
    {
        public CaraBusException(string alreadyRunning, Exception innerException = null)
            :base(alreadyRunning, innerException)
        {
            
        }
    }
}
