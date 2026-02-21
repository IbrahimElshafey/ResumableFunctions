# Engine Modules for ResumableWorkflows
## 1. ResumableWorkflows.Engine.Modules.SignalReceiver.Abstraction
The SignalReceiver module is responsible for receiving incoming signals from external sources and passing them to the engine for processing. It provides an abstraction layer that allows different implementations for receiving signals, such as WebApi, gRPC, and IPC. This module ensures flexibility in integrating with various communication protocols and allows for easy extensibility to support new signal reception mechanisms.

## 2. ResumableWorkflows.Engine.Modules.SignalsStore.Abstraction
The SignalsStore module is responsible for storing and retrieving received signals. It provides an abstraction layer that allows different storage implementations, such as EntityFrameworkCore for relational databases, MongoDB for NoSQL databases, and an in-memory storage option for testing and development purposes. This module enables the engine to persist and manage signals efficiently, regardless of the underlying storage technology.

## 3. ResumableWorkflows.Engine.Modules.WaitsStore.Abstraction
The WaitsStore module is responsible for storing and retrieving wait conditions for resumable workflows. It provides an abstraction layer that allows different storage implementations, similar to the SignalsStore module. The WaitsStore module abstracts away the details of wait condition persistence, making it easier to switch between different storage options based on the project's requirements.

## 4. ResumableWorkflows.Engine.Modules.WorkflowStateStore.Abstraction
The WorkflowStateStore module is responsible for persisting and retrieving the state of workflow instances. It provides an abstraction layer that supports different storage implementations, enabling the engine to store workflow state in various databases or storage systems. This module ensures that the engine can seamlessly persist and restore workflow state, allowing for fault tolerance and resumability.

## 5. ResumableWorkflows.Engine.Modules.WorkflowRunner.Abstraction
The WorkflowRunner module is responsible for executing workflow instances based on their pre-compiled definitions inside the DLLs. It loads the workflow DLLs and executes the workflow code directly, leveraging the compiled nature of the definitions for optimal performance. This module encapsulates the core execution logic of the engine and ensures the proper execution of the workflow instances based on their defined steps and logic.

## 6. ResumableWorkflows.Engine.Modules.WorkflowScheduler.Abstraction
The WorkflowScheduler module is responsible for scheduling and managing the execution of workflow instances based on their specified triggers and schedules. It provides an abstraction layer that enables different scheduling implementations, such as using built-in scheduling mechanisms or integrating with external scheduling systems. This module ensures that workflows are triggered and executed at the appropriate times, based on their defined schedules and dependencies.

## 7. ResumableWorkflows.Engine.Modules.CommandSender.Abstraction
The CommandSender module is responsible for sending commands or messages to external systems or services as part of workflow execution. It provides an abstraction layer that supports different implementation approaches, such as using the outbox pattern for reliable message delivery or direct sending for simple scenarios. This module abstracts away the complexities of command sending and allows for flexible integration with various messaging systems or service invocation mechanisms.

## 8. ResumableWorkflows.Engine.Extensions.Modules.Diagnostics
The Diagnostics module is an extension module that provides diagnostic and monitoring capabilities for the workflow engine. It allows for capturing and analyzing runtime information, such as performance metrics, logs, and execution traces. This module helps in troubleshooting, performance optimization, and gaining insights into the behavior of the engine and individual workflow instances.

## 9. ResumableWorkflows.Engine.Extensions.Modules.WorkflowLoader
The WorkflowLoader module is an extension module responsible for loading workflow definitions from DLLs and managing the versioning of those definitions. It enables the engine to run different versions of the same workflow definition simultaneously. The module abstracts the process of loading workflow definitions from DLLs and provides mechanisms for versioning and managing the loaded definitions. This allows for seamless updates and backwards compatibility of workflow definitions within the engine.

## 10. ResumableWorkflows.Engine.Extensions.Modules.WorkflowRegistery
The WorkflowRegistery module is an extension module that maintains a registry of available workflow types and their corresponding definitions. It provides an abstraction layer for registering and retrieving workflow types, allowing the engine to dynamically discover and instantiate workflows based on their registered types. This module promotes loose coupling and enables the engine to support a wide range of workflow scenarios.
