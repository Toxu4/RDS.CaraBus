namespace RDS.CaraBus
{
    public class SubscribeOptions
    {
        public bool Exclusive { get; set; }
        public string Scope { get; set; }
        /// <summary>
        /// Maximum number of concurrently working handlers
        /// </summary>
        public ushort MaxConcurrentHandlers { get; set; }
    }
}