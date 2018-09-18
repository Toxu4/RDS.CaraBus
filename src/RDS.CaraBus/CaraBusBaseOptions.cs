using System;

namespace RDS.CaraBus
{
    public class CaraBusBaseOptions
    {
        public byte MaxDegreeOfParallelism { get; set; } = 4;


        public TimeSpan? TimeoutOnStop { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The number of milliseconds to wait subscribed task before grow in memory queue, (-1) to wait indefinitely
        /// </summary>
        public int QueueGrowTimeout { get; set; } = 1000;

        public bool AutoStart { get; set; }
    }
}
