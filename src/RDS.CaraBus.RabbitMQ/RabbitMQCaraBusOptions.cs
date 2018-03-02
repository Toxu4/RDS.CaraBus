namespace RDS.CaraBus.RabbitMQ
{
    public class RabbitMQCaraBusOptions : CaraBusBaseOptions
    {
        /// <summary>
        /// The connection string. See https://www.rabbitmq.com/uri-spec.html for more information.
        /// </summary>
        public string ConnectionString { get; set; }
    }
}
