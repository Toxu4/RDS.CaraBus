using System;

namespace RDS.CaraBus
{
    public class CaraBusBaseOptions
    {
        public byte MaxDegreeOfParallelism { get; set; } = 4;

        public TimeSpan? TimeoutOnStop { get; set; } = TimeSpan.FromSeconds(30);

        public bool AutoStart { get; set; }
    }
}
