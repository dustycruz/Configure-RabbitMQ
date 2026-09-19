using System.Globalization;
using System.Text;
using System.Text.Json;
using Contracts;
using RabbitMQ.Client;

// ---------------------------------------------------------------------------
// Producer — publishes OrderPlaced events to the durable "order.placed" queue.
//
// Usage:
//   dotnet run --project Producer          -> publishes 1 order
//   dotnet run --project Producer -- 10    -> publishes 10 orders
// ---------------------------------------------------------------------------

const string QueueName = "order.placed";

// How many orders to publish on this run (first CLI argument, default 1).
int orderCount = 1;
if (args.Length > 0 && (!int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out orderCount) || orderCount < 1))
{
    Console.Error.WriteLine($"Invalid order count '{args[0]}'. Pass a positive whole number, e.g.: dotnet run --project Producer -- 10");
    return 1;
}

var factory = new ConnectionFactory
{
    HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost",
    Port = int.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out var port) ? port : 5672,
    UserName = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest",
    Password = Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "guest"
};

// Both connection and channel are IAsyncDisposable in RabbitMQ.Client 7.x.
await using var connection = await factory.CreateConnectionAsync();
await using var channel = await connection.CreateChannelAsync();

// Declare the queue. Declaring is idempotent: if the UI already created
// "order.placed" with the same settings, this is a no-op.
await channel.QueueDeclareAsync(
    queue: QueueName,
    durable: true,      // survives a broker restart
    exclusive: false,   // usable by other connections
    autoDelete: false,  // stays around when the last consumer disconnects
    arguments: null);

Console.WriteLine($"Connected to {factory.HostName}:{factory.Port}. Publishing {orderCount} order(s) to '{QueueName}'...");

var random = new Random();

for (var i = 0; i < orderCount; i++)
{
    var order = new OrderPlaced(
        OrderId: Guid.NewGuid(),
        StudentId: $"S{random.Next(1000, 9999)}",
        Total: Math.Round((decimal)(random.NextDouble() * 500 + 10), 2),
        PlacedAtUtc: DateTime.UtcNow);

    var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(order));

    var properties = new BasicProperties
    {
        Persistent = true,                        // message written to disk
        MessageId = order.OrderId.ToString(),     // lets consumers de-duplicate
        ContentType = "application/json",
        Type = nameof(OrderPlaced),
        Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
    };

    // Default exchange ("") routes by queue name, so routingKey == queue name.
    await channel.BasicPublishAsync(
        exchange: string.Empty,
        routingKey: QueueName,
        mandatory: false,
        basicProperties: properties,
        body: body);

    Console.WriteLine($"Published OrderPlaced {order.OrderId} | student={order.StudentId} total={order.Total:0.00} placedAtUtc={order.PlacedAtUtc:O}");
}

Console.WriteLine($"Done. {orderCount} order(s) published.");
return 0;