using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace RabbitMQ.Example.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {

         private static string _queueName = "rpc_queue";
         private static string _responseQueueName = "rpc_response_queue";


        private static string queueName = "task_queue";
        private static string responseQueue = "response_queue";
        private static string rabbitMqUri = "amqp://guest:guest@localhost:5672/";
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;

        public WeatherForecastController(ILogger<WeatherForecastController> logger)
        {
            _logger = logger;
        }

        [HttpGet(Name = "GetWeatherForecast")]
        public IActionResult GetWeatherForecast(int id)
        {


            // Set up RabbitMQ connection and channel
            var factory = new ConnectionFactory() { HostName = "localhost",UserName="guest",Password="guest",VirtualHost="/"};
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueDeclare(queue: _queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);

            var replyQueue = channel.QueueDeclare(_responseQueueName, false, true, false, null);
            var correlationId = Guid.NewGuid().ToString();

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (sender, e) =>
            {
                if (e.BasicProperties.CorrelationId == correlationId)
                {
                    var response = Encoding.UTF8.GetString(e.Body.ToArray());
                    Console.WriteLine($"Received response: {response}");
                }
            };

            channel.BasicConsume(queue: replyQueue.QueueName, autoAck: true, consumer: consumer);

            string message = "Request Data";
            var properties = channel.CreateBasicProperties();
            properties.ReplyTo = replyQueue.QueueName;
            properties.CorrelationId = correlationId;

            var messageBytes = Encoding.UTF8.GetBytes(message);
            channel.BasicPublish(exchange: "", routingKey: _queueName, basicProperties: properties, body: messageBytes);

            Console.WriteLine("Sent request to queue... Waiting for response...");

            while (true)
            {
                Thread.Sleep(100); 
            }
        

            
        }



        [HttpPost] 
        public ActionResult<string> Post([FromBody] string message)
        {
            var factory = new ConnectionFactory() { Uri = new Uri(rabbitMqUri) };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
            channel.QueueDeclare(queue: responseQueue, durable: false, exclusive: false, autoDelete: true, arguments: null);

            var correlationId = Guid.NewGuid().ToString();

            var consumer = new EventingBasicConsumer(channel);
            string result = null;

            consumer.Received += (sender, e) =>
            {
                if (e.BasicProperties.CorrelationId == correlationId)
                {
                    result = Encoding.UTF8.GetString(e.Body.ToArray());
                }
            };

            channel.BasicConsume(queue: responseQueue, autoAck: true, consumer: consumer);

            var props = channel.CreateBasicProperties();
            props.ReplyTo = responseQueue;
            props.CorrelationId = correlationId;

            byte[] body = Encoding.UTF8.GetBytes(message);
            channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: props, body: body);
            Console.WriteLine("Request sent: " + message);

            
            while (result == null)
            {
                Thread.Sleep(100);  
            }

            return Ok(result);
        }
    }
}
