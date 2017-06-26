using System;

namespace RDS.CaraBus
{
    public class StopOptions
    {
        public bool WaitForSubscribers { get; set; } = true;

        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    }
}
