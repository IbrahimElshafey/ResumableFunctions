# Why not message queues like RabbitMq or Kafka?
* I need simple solution for communication between services
* I need a solution that prevent a single point of failure
* NetMq which is a .net version of ZeroMq may be a solution

I changed my mind and I'll use MassTransit for communication between services. I'll use it to send messages between services and to send messages to the engine. I'll use it to send messages to the engine to resume functions and to send messages to the engine to wait for a condition.