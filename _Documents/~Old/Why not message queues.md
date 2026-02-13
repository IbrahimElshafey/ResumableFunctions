# Why not message queues like RabbitMq or Kafka?
* I need simple solution for communication between services
* I need a solution that prevent a single point of failure
* NetMq which is a .net version of ZeroMq may be a solution
* Finall decision is to use SQLite Outbox/Inbox pattern for communication between services, and use HTTP calls for signaling and resumption. This approach provides reliability and simplicity without introducing additional dependencies or complexity of message queues.