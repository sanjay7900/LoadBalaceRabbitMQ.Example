using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System.Text;

namespace Worker.two
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private static string queueName = "task_queue";
        private static string rabbitMqUri = "amqp://guest:guest@localhost:5672/";
        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {


                var factory = new ConnectionFactory() { Uri = new Uri(rabbitMqUri) };

                try
                {
                    using var connection = factory.CreateConnection();
                    using var channel = connection.CreateModel();

                    channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
                    Console.WriteLine("Worker started and waiting for messages.");

                    var consumer = new EventingBasicConsumer(channel);
                    consumer.Received += (sender, e) =>
                    {
                        var message = Encoding.UTF8.GetString(e.Body.ToArray());
                        Console.WriteLine("Received request: " + message);

                        
                        string responseMessage = $"Processed: {message} from worker two";

                        // Send the response to the reply-to queue

                        var props = e.BasicProperties;
                        var replyTo = props.ReplyTo;
                        var correlationId = props.CorrelationId;

                        var responseProps = channel.CreateBasicProperties();
                        responseProps.CorrelationId = correlationId;

                        // Send the response back
                        channel.BasicPublish(
                            exchange: "",
                            routingKey: replyTo,
                            basicProperties: responseProps,
                            body: Encoding.UTF8.GetBytes(responseMessage)
                        );
                        Console.WriteLine("Response sent: " + responseMessage);
                    };

                    channel.BasicConsume(queue: queueName, autoAck: true, consumer: consumer);

                    // Block the service to keep it running while waiting for messages
                    //await Task.Delay(Timeout.Infinite, stoppingToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An error occurred while connecting to RabbitMQ: " + ex.Message);
                }



                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                }
               // await Task.Delay(1000, stoppingToken);
            }
        }
    }

}
