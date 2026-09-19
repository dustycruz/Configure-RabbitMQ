# OrderEvents

Publishes and consumes an `OrderPlaced` event through a durable RabbitMQ queue
(`order.placed`) using manual acknowledgement.

| Project     | What it does                                              |
|-------------|-----------------------------------------------------------|
| `Contracts` | The shared `OrderPlaced` record.                          |
| `Producer`  | Publishes persistent `OrderPlaced` events to the queue.   |
| `Consumer`  | Consumes the queue (prefetch 10) and acks each message.   |

Requires the [.NET SDK 8.0+](https://dotnet.microsoft.com/download) and Docker.

## 1. Start RabbitMQ

```bash
docker run -d --name rabbitmq \
  -p 5672:5672 -p 15672:15672 \
  rabbitmq:3.13-management
```

```bash
docker ps
```

`5672` is the AMQP port the apps use; `15672` serves the Management UI at
<http://localhost:15672> (log in with **guest** / **guest**).

Both apps declare the durable `order.placed` queue on startup, so creating it by
hand in the UI is optional.

## 2. Build

```bash
dotnet build
```

## 3. Run the consumer

In its own terminal — runs until `Ctrl+C`:

```bash
dotnet run --project Consumer
```

## 4. Run the producer

In a second terminal:

```bash
dotnet run --project Producer        # publish 1 order
dotnet run --project Producer -- 10  # publish 10 orders
```

## 5. Stop/restart test

Slow the consumer down so messages are still **Unacked** when you stop it:

```bash
PROCESS_DELAY_MS=2000 dotnet run --project Consumer
```

Stop it mid-run. The Unacked messages return to **Ready** instead of being lost,
and are reprocessed with a `[REDELIVERED]` marker when you start it again.

## Stop the broker

```bash
docker stop rabbitmq && docker rm rabbitmq
```

## Optional environment variables

| Variable           | Default     | Meaning                                    |
|--------------------|-------------|--------------------------------------------|
| `RABBITMQ_HOST`    | `localhost` | Broker hostname                            |
| `RABBITMQ_PORT`    | `5672`      | AMQP port                                  |
| `RABBITMQ_USER`    | `guest`     | Username                                   |
| `RABBITMQ_PASS`    | `guest`     | Password                                   |
| `PROCESS_DELAY_MS` | `500`       | Consumer only — simulated work per message |
