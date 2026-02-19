# ResumableWorkflows — Architecture & Interface Specification

---

## Table of Contents

1. [What is ResumableWorkflows?](#1-what-is-resumableworkflows)
2. [The Three Layers](#2-the-three-layers)
3. [How It All Works — End to End](#3-how-it-all-works--end-to-end)
4. [ResumableWorkflows.Abstractions](#4-resumableworkflowsabstractions)
5. [ResumableWorkflows.Client](#5-resumableworkflowsclient)
6. [ResumableWorkflows.Definition](#6-resumableworkflowsdefinition)
7. [ResumableWorkflows.Engine](#7-resumableworkflowsengine)
8. [The Definition File (workflow.json)](#8-the-definition-file-workflowjson)
9. [Code Generation Pipeline](#9-code-generation-pipeline)
10. [Communication Model — Single Endpoint](#10-communication-model--single-endpoint)
11. [Signals and Commands](#11-signals-and-commands)
12. [Reliability — Outbox/Inbox](#12-reliability--outboxinbox)
13. [Versioning](#13-versioning)
14. [Deployment Scenarios](#14-deployment-scenarios)

---

## 1. What is ResumableWorkflows?

ResumableWorkflows is a workflow orchestration engine for .NET. It lets developers write
long-running, stateful business processes as plain C# methods that can pause, wait for
external events, resume hours or days later, coordinate multiple services, and survive
process restarts.

A workflow might look like this:

```csharp
[Workflow("LeaveApproval", Version = 1.0)]
public class LeaveApprovalWorkflow : WorkflowContainer
{
    public override async IAsyncEnumerable<WorkflowWait> Execute()
    {
        // Wait for an employee to submit a leave request
        yield return WaitForSignal<LeaveRequest>("SubmitLeave")
            .MatchIf(r => r.EmployeeId == EmployeeId)
            .AfterMatch(r => this.Request = r);

        // Tell the manager's service to send a notification
        yield return SendCommand<NotifyManager>("NotifyManager",
            new NotifyManagerRequest { ManagerId = Request.ManagerId, LeaveId = Request.Id });

        // Wait for manager approval — could take days
        yield return WaitForSignal<ApprovalResult>("ManagerDecision")
            .MatchIf(r => r.LeaveId == Request.Id)
            .AfterMatch(r => this.Decision = r);

        if (Decision.Approved)
            yield return SendCommand<UpdateCalendar>("UpdateCalendar", ...);
        else
            yield return SendCommand<NotifyRejection>("NotifyEmployee", ...);
    }
}
```

The engine persists the workflow state after each `yield return`. If the server crashes,
the workflow resumes exactly where it left off when the server restarts.

---

## 2. The Three Layers

ResumableWorkflows is split into three NuGet packages plus the developer's workflow DLL.
They are completely decoupled — **no package references each other except through
`ResumableWorkflows.Abstractions`**, and even that is minimal.

```
┌──────────────────────────────────────────────────────────┐
│               Developer's Workflow DLL                    │
│                                                          │
│  Contains: workflow classes, copied .workflow.json files  │
│  References: ResumableWorkflows.Definition                │
│  Loaded by: ResumableWorkflows.Engine at runtime          │
└────────────────────────┬─────────────────────────────────┘
                         │ loaded at runtime
                         ▼
┌──────────────────────────────────────────────────────────┐
│             ResumableWorkflows.Engine                     │
│                                                          │
│  The orchestrator. Hosts workflow DLLs, tracks instances, │
│  matches signals, sends commands, manages versions.       │
│  References: Abstractions, Reliability, Definition        │
└──────────────────────────────────────────────────────────┘
         ▲                                    │
         │ signals (one endpoint)             │ commands (one endpoint)
         │                                    ▼
┌──────────────────────────────────────────────────────────┐
│  Developer's Service (Web API / gRPC / DLL)              │
│                                                          │
│  Contains: business logic methods decorated with          │
│  [WorkflowSignal] and [WorkflowCommand]                  │
│  References: ResumableWorkflows.Client                    │
└──────────────────────────────────────────────────────────┘

Shared by all:
┌──────────────────────────────────────────────────────────┐
│          ResumableWorkflows.Abstractions                  │
│  Shared message contracts, enums, configuration models.   │
│  Zero external dependencies. Zero logic.                  │
└──────────────────────────────────────────────────────────┘
┌──────────────────────────────────────────────────────────┐
│          ResumableWorkflows.Reliability                   │
│  Outbox/inbox implementation, SQLite, retry, transports.  │
│  Internal shared package — not published to developers.   │
└──────────────────────────────────────────────────────────┘
```

### Dependency Rules

| Package | References | Referenced By |
|---------|-----------|---------------|
| **Abstractions** | Nothing | Reliability, Client, Definition, Engine |
| **Reliability** | Abstractions | Client, Definition, Engine |
| **Client** | Abstractions, Reliability | Developer's services |
| **Definition** | Abstractions, Reliability | Developer's workflow DLLs, Engine |
| **Engine** | Abstractions, Reliability, Definition | Nobody (it's the host) |

**Client and Definition never reference each other.** The only bridge between them is the
`.workflow.json` definition file, which is a copied file — not a binary reference.

**Engine and Client never reference each other.** They communicate over the network (HTTP,
gRPC, named pipes) through a single endpoint on each side.

**Abstractions has zero logic.** Only data shapes, enums, and configuration models.
All outbox/inbox implementation lives in Reliability.

---

## 3. How It All Works — End to End

Let's walk through a complete scenario: an order service signals that an order was approved,
and the engine tells an email service to send a confirmation.

### Step-by-step:

```
                    THE DEVELOPER'S WORLD (build time)
═══════════════════════════════════════════════════════════════

 ① Developer writes OrderService with [WorkflowSignal] on ApproveOrder()
    → Client source generator produces:
      • OrderService.workflow.json (the definition)
      • A single receiver endpoint: POST /_workflow/receive
      • A definition endpoint: GET /_workflow/definition
      • Internal routing table: message.Endpoint → local method

 ② Developer writes EmailService with [WorkflowCommand] on SendConfirmation()
    → Same generation: EmailService.workflow.json, receiver endpoint, definition endpoint

 ③ Developer copies both .workflow.json files into their workflow project
    → Definition source generator reads the JSON and produces:
      • OrderServiceClient (knows how to wait for OrderApproved signal)
      • EmailServiceClient (knows how to send SendConfirmation command via outbox)

 ④ Developer writes OrderWorkflow using the generated types
    → Compiles into OrderWorkflow.dll


                    THE ENGINE'S WORLD (runtime)
═══════════════════════════════════════════════════════════════

 ⑤ Engine starts, loads OrderWorkflow.dll via AssemblyLoadContext
    → Discovers workflows, registers wait patterns

 ⑥ Engine calls GET /_workflow/definition on each registered service
    → Learns what signals/commands each service supports
    → Verifies compatibility with workflow expectations

 ⑦ A user approves an order in the OrderService UI
    → OrderService.ApproveOrder() executes
    → The method is decorated with [WorkflowSignal]
    → Client infrastructure captures the input + output
    → Writes a WorkflowMessage to the local SQLite outbox
    → Outbox dispatcher sends it to the engine's single endpoint

 ⑧ Engine receives the signal at POST /_workflow/receive
    → Inbox deduplicates (checks message ID)
    → WaitMatcher finds the OrderWorkflow instance waiting for "OrderApproved"
    → Wait status updated: Active → SignalReceived (persisted)
    → ACK returned to sender immediately
    → WorkflowExecutor resumes the workflow from saved state
    → Wait status updated: SignalReceived → Completed
    → Workflow reaches: SendCommand("SendConfirmation", ...)
    → Engine writes the command to its own outbox

 ⑨ Engine's outbox dispatcher sends the command
    → Calls POST /_workflow/receive on EmailService (single endpoint)
    → EmailService's internal router dispatches to SendConfirmation()
    → Email is sent, ACK returned

 ⑩ Workflow continues to next yield, or completes
```

---

## 4. ResumableWorkflows.Abstractions

The thinnest layer. Contains only types that both sides need to understand messages.
**Zero external dependencies. Zero logic. Only data shapes and enums.**

### Message Contracts

Every piece of communication between Engine and Services uses this message format,
regardless of whether it's a signal, command, or response.

```csharp
namespace ResumableWorkflows.Abstractions.Messages;

/// <summary>
/// The universal message envelope. Every signal, command, and response
/// travels inside this wrapper. Both the Engine and Client understand
/// this format — it's the common language.
/// </summary>
public class WorkflowMessage
{
    /// <summary>Globally unique ID. Used for deduplication in the inbox.</summary>
    public string MessageId { get; set; }

    /// <summary>
    /// For Response messages only. Contains the MessageId of the command
    /// that triggered this response. The engine uses this to find the
    /// workflow instance waiting for this command's result.
    /// Null for Signal and Command messages.
    /// </summary>
    public string? InResponseToMessageId { get; set; }

    /// <summary>Who sent this message. Matches the ServiceId in registration.</summary>
    public string SourceServiceId { get; set; }

    /// <summary>What kind of message this is.</summary>
    public WorkflowMessageType MessageType { get; set; }

    /// <summary>
    /// The logical name of the target action (e.g., "OrderApproved", "SendInvoice").
    /// The receiver's internal router uses this to dispatch to the correct method.
    /// </summary>
    public string Endpoint { get; set; }

    /// <summary>JSON-serialized payload — the actual data.</summary>
    public string Payload { get; set; }

    /// <summary>Metadata: version, timestamp, HMAC signature, custom headers.</summary>
    public WorkflowMessageHeaders Headers { get; set; }
}

public enum WorkflowMessageType
{
    Signal,
    Command,
    Response
}

public class WorkflowMessageHeaders
{
    public string Version { get; set; }
    public string Timestamp { get; set; }
    public string Hmac { get; set; }
    public Dictionary<string, string> Custom { get; set; }
}

/// <summary>
/// The standard response to any received message.
/// Returned by the single receiver endpoint.
/// </summary>
public class WorkflowMessageResponse
{
    /// <summary>True if the receiver accepted the message.</summary>
    public bool Acknowledged { get; set; }

    /// <summary>True if this message was already processed (inbox hit).</summary>
    public bool DuplicateDetected { get; set; }

    /// <summary>Optional response payload (e.g., command result).</summary>
    public string Payload { get; set; }

    /// <summary>Error details if processing failed.</summary>
    public string Error { get; set; }
}
```

### Configuration Models

```csharp
namespace ResumableWorkflows.Abstractions.Configuration;

/// <summary>
/// Shared outbox configuration. Used by both Client and Engine
/// since both sides send messages and need reliability.
/// </summary>
public class WorkflowOutboxOptions
{
    public string DatabasePath { get; set; } = "workflow_outbox.db";
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);
    public int DefaultMaxRetries { get; set; } = 5;
    public TimeSpan DefaultMessageTTL { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromSeconds(2);
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(5);
    public int BatchSize { get; set; } = 50;
    public TimeSpan SentRetention { get; set; } = TimeSpan.FromDays(7);
}

/// <summary>
/// Shared inbox configuration. Used by both Client and Engine
/// since both sides receive messages and need deduplication.
/// </summary>
public class WorkflowInboxOptions
{
    public string DatabasePath { get; set; } = "workflow_inbox.db";
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);
}

public class WorkflowSecurityOptions
{
    public string HmacAlgorithm { get; set; } = "HMAC-SHA256";
    public Dictionary<string, string> SharedSecrets { get; set; } = new();
}
```

### Service Registration

```csharp
namespace ResumableWorkflows.Abstractions.Registration;

/// <summary>
/// Describes a service's capabilities. The Engine uses this to know:
/// - Where to send commands (the single endpoint URL)
/// - What signals/commands the service supports
/// - What protocol to use
/// </summary>
public class WorkflowServiceInfo
{
    public string ServiceId { get; set; }

    /// <summary>
    /// The single connection point. The engine sends ALL messages here.
    /// Example: "https://order-service:5000/_workflow/receive"
    /// </summary>
    public string EndpointUrl { get; set; }

    /// <summary>
    /// Where to fetch the service's definition file.
    /// Example: "https://order-service:5000/_workflow/definition"
    /// </summary>
    public string DefinitionUrl { get; set; }

    public string Protocol { get; set; }  // "http", "grpc", "namedpipe", "inprocess"
    public string[] SignalNames { get; set; }
    public string[] CommandNames { get; set; }
}
```

---

## 5. ResumableWorkflows.Client

Referenced by developer services (Web APIs, gRPC services, or standard DLLs). This package
does three things:

1. **Provides attributes** (`[WorkflowSignal]`, `[WorkflowCommand]`) for the developer to
   decorate their methods.
2. **Source-generates infrastructure** at build time:
   - A `.workflow.json` definition file describing the service's contract
   - A single receiver endpoint (`POST /_workflow/receive`) that accepts all messages from
     the engine and routes them internally
   - A definition endpoint (`GET /_workflow/definition`) that serves the JSON file
3. **Provides the outbox/inbox reliability layer** so signals reach the engine even if the
   network is temporarily down.

### What the developer writes:

```csharp
// This is a normal ASP.NET service. The developer adds ResumableWorkflows.Client
// and decorates methods. That's it — everything else is generated.

public class OrderService
{
    [WorkflowSignal("OrderApproved")]
    public OrderResult ApproveOrder(ApproveOrderRequest request)
    {
        // Normal business logic
        var order = _db.GetOrder(request.OrderId);
        order.Status = "Approved";
        _db.Save(order);
        return new OrderResult { Success = true, OrderId = order.Id };
    }

    [WorkflowCommand("CancelOrder")]
    public void CancelOrder(CancelOrderRequest request)
    {
        // The engine can tell this service to cancel an order
        var order = _db.GetOrder(request.OrderId);
        order.Status = "Cancelled";
        _db.Save(order);
    }

    [WorkflowSignal("InventoryReserved")]
    [WorkflowCommand("ReserveInventory")]
    public ReserveResult ReserveInventory(ReserveRequest request)
    {
        // This method is BOTH a signal and a command.
        // As a signal: when called normally, it notifies the engine.
        // As a command: the engine can ask this service to reserve inventory.
        // ...
    }
}
```

### What the source generator produces:

#### a) The definition file — `OrderService.workflow.json`

Automatically generated at build time. Describes every signal and command the service
exposes. This file is the contract between the service and any workflow that wants to
interact with it. See Section 8 for the full format.

#### b) The single receiver endpoint

```
POST /_workflow/receive    ← Engine sends ALL messages here
GET  /_workflow/definition ← Returns the .workflow.json file
```

The engine never calls individual service endpoints. It sends every command to
`/_workflow/receive` with the `Endpoint` field in the message body indicating which
method to invoke. The generated router dispatches internally.

#### c) Internal routing table

The source generator builds a lookup table mapping `Endpoint` names to local methods.
When a message arrives at `/_workflow/receive`, the router finds the right method and
calls it.

### Attributes

```csharp
namespace ResumableWorkflows.Client.Attributes;

/// <summary>
/// Marks a method as producing a workflow signal. When this method executes,
/// the Client infrastructure captures its input and output, wraps them in a
/// WorkflowMessage, and sends it to the engine via the outbox.
///
/// "Something happened" — the service tells the engine about an event.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class WorkflowSignalAttribute : Attribute
{
    public string Name { get; }
    public WorkflowSignalAttribute(string name) => Name = name;
}

/// <summary>
/// Marks a method as accepting a workflow command. The engine can tell this
/// service to execute this method by sending a command message to the
/// service's single endpoint.
///
/// "Do something" — the engine instructs the service to act.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class WorkflowCommandAttribute : Attribute
{
    public string Name { get; }
    public WorkflowCommandAttribute(string name) => Name = name;
}
```

### Service Setup

```csharp
namespace ResumableWorkflows.Client;

public static class WorkflowClientExtensions
{
    /// <summary>
    /// Registers all ResumableWorkflows client services: outbox dispatcher,
    /// inbox store, signal interceptor, message router, and background workers.
    /// </summary>
    public static IServiceCollection AddWorkflowClient(
        this IServiceCollection services,
        Action<WorkflowClientOptions> configure);

    /// <summary>
    /// Maps the two auto-generated endpoints:
    ///   POST /_workflow/receive     — single entry point for engine messages
    ///   GET  /_workflow/definition  — serves the workflow.json definition
    /// </summary>
    public static IApplicationBuilder UseWorkflowClient(
        this IApplicationBuilder app);
}

public class WorkflowClientOptions
{
    /// <summary>Unique identifier for this service (e.g., "order-service").</summary>
    public string ServiceId { get; set; }

    /// <summary>
    /// The engine's single endpoint URL. All signals go here.
    /// Example: "https://workflow-engine:6000/_workflow/receive"
    /// </summary>
    public string EngineUrl { get; set; }

    /// <summary>Which transport to use when sending signals to the engine.</summary>
    public WorkflowTransportType DefaultTransport { get; set; }

    public WorkflowOutboxOptions Outbox { get; set; } = new();
    public WorkflowInboxOptions Inbox { get; set; } = new();
    public WorkflowSecurityOptions Security { get; set; } = new();
}

public enum WorkflowTransportType
{
    Http,
    Grpc,
    NamedPipe,
    InProcess
}
```

### Internal Interfaces

These are **not visible to the developer**. They exist inside the Client package and are
used by the generated code and the background workers.

```csharp
namespace ResumableWorkflows.Client.Internal;

/// <summary>
/// The single entry point for all messages from the engine. Generated by the
/// source generator. Handles:
///   1. HMAC verification
///   2. Inbox deduplication check
///   3. Internal routing to the correct [WorkflowCommand] method
///   4. Writing to inbox (same transaction as processing)
///   5. Returning ACK with optional response payload
/// </summary>
internal interface IWorkflowReceiver
{
    Task<WorkflowMessageResponse> Receive(WorkflowMessage message);
}

/// <summary>
/// Maps endpoint names to local methods. Built by the source generator from
/// all [WorkflowCommand] and [WorkflowSignal] decorated methods.
///
/// Example routing table:
///   "CancelOrder"       → OrderService.CancelOrder(...)
///   "ReserveInventory"  → InventoryService.ReserveInventory(...)
/// </summary>
internal interface IWorkflowMessageRouter
{
    Task<object?> Route(string endpoint, string jsonPayload);
    bool CanHandle(string endpoint);
    IReadOnlyList<string> RegisteredEndpoints { get; }
}

/// <summary>
/// Intercepts [WorkflowSignal] method calls. When a decorated method finishes,
/// the interceptor captures the input + output, builds a WorkflowMessage, and
/// writes it to the outbox.
///
/// The developer's method runs normally — the interception is invisible.
/// </summary>
internal interface IWorkflowSignalInterceptor
{
    Task OnMethodCompleted(string signalName, object input, object output);
}

/// <summary>
/// Serves the .workflow.json definition file. The engine calls this endpoint
/// to discover what the service supports.
///
/// GET /_workflow/definition → returns the JSON content
/// </summary>
internal interface IWorkflowDefinitionEndpoint
{
    Task<string> GetDefinitionJson();
}
```

### Outbox and Inbox (from ResumableWorkflows.Reliability)

The Client does not implement its own outbox/inbox. It uses the shared implementation
from the `ResumableWorkflows.Reliability` package. The interfaces and SQLite-backed
implementations are provided by Reliability; the Client simply configures and uses them.

```csharp
// These interfaces live in ResumableWorkflows.Reliability.
// Shown here for context — the Client uses them, it doesn't define them.

namespace ResumableWorkflows.Reliability;

/// <summary>
/// Persists outgoing messages in SQLite until they are successfully delivered.
/// The outbox guarantees that if the business operation committed, the message
/// will eventually be sent — even if the process crashes immediately after.
/// </summary>
public interface IWorkflowOutboxStore
{
    Task Insert(WorkflowOutboxMessage message);
    Task<IReadOnlyList<WorkflowOutboxMessage>> GetPending(int batchSize, DateTime olderThan);
    Task UpdateStatus(string messageId, string status);
    Task MarkAsSent(string messageId, DateTime sentAt);
    Task RecordFailure(string messageId, string error);
    Task ScheduleRetry(string messageId, DateTime nextRetryAt);
    Task CleanupSent(TimeSpan retention);
    Task ExpireStale();
}

/// <summary>
/// Tracks which messages have already been processed, preventing duplicates.
/// When the engine retries a command that was already handled, the inbox
/// returns the cached response without re-executing the method.
/// </summary>
public interface IWorkflowInboxStore
{
    Task<WorkflowInboxRecord?> Get(string messageId);
    Task Insert(WorkflowInboxRecord record);
    Task Cleanup();
}

/// <summary>
/// Background worker that polls the outbox and sends pending messages.
/// Runs continuously while the service is alive.
/// </summary>
public interface IWorkflowOutboxDispatcher
{
    Task Start(CancellationToken ct);
}

/// <summary>
/// Pluggable transport. The outbox dispatcher uses this to actually send
/// the message over the wire. Different implementations for HTTP, gRPC, etc.
/// </summary>
public interface IWorkflowTransport
{
    Task<WorkflowMessageResponse> Send(WorkflowMessage message, string destinationUrl);
    string Protocol { get; }
}
```

---

## 6. ResumableWorkflows.Definition

Referenced by the developer's **workflow DLL**. This package provides:

1. **Base classes and wait types** for writing workflows.
2. **A source generator** that reads `.workflow.json` files and produces typed service
   clients — the workflow author uses these to interact with services.
3. **The base outbox client implementation** (via `ResumableWorkflows.Reliability`) that
   the generated clients use internally to ensure commands reach services reliably.

### What the developer writes:

```csharp
// The workflow project references ResumableWorkflows.Definition
// and includes copied .workflow.json files from the services it talks to.

[Workflow("OrderProcessing", Version = 1.0)]
public class OrderWorkflow : WorkflowContainer
{
    // These are populated by the engine when resuming
    public Guid OrderId { get; set; }
    public OrderResult ApprovalResult { get; set; }

    public override async IAsyncEnumerable<WorkflowWait> Execute()
    {
        // Wait for a signal from the OrderService
        yield return WaitForSignal<OrderApprovedSignal>("OrderApproved")
            .MatchIf(s => s.Input.OrderId == OrderId)
            .AfterMatch(s => ApprovalResult = s.Output);

        // Send a command to the EmailService
        yield return SendCommand("SendConfirmation",
            new SendConfirmationRequest { OrderId = OrderId, Email = "..." });
    }
}
```

The `OrderApprovedSignal` type and `SendConfirmationRequest` type are **generated from
the .workflow.json files** by the Definition source generator. The developer gets full
IntelliSense on these types.

### Workflow Base Class and Attributes

```csharp
namespace ResumableWorkflows.Definition;

/// <summary>
/// Base class for all workflow definitions. The engine loads classes that
/// extend this and calls Execute() to run the workflow. State is serialized
/// after each yield and restored on resume.
///
/// All public properties are persisted as workflow state. Use only
/// serializable types.
/// </summary>
public abstract class WorkflowContainer
{
    /// <summary>
    /// The workflow's unique instance ID. Set by the engine.
    /// </summary>
    public string InstanceId { get; internal set; }

    /// <summary>
    /// Define the workflow as a sequence of waits. Each yield return
    /// pauses the workflow and saves its state. The engine resumes
    /// execution when the waited condition is met.
    /// </summary>
    public abstract IAsyncEnumerable<WorkflowWait> Execute();
}
```

```csharp
namespace ResumableWorkflows.Definition.Attributes;

/// <summary>
/// Marks a class as a workflow definition. The engine discovers these
/// when loading a workflow DLL.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class WorkflowAttribute : Attribute
{
    public string Name { get; }
    public double Version { get; }
    public WorkflowAttribute(string name, double version = 1.0)
    {
        Name = name;
        Version = version;
    }
}

/// <summary>
/// Marks a method within a workflow as a named step. Used for
/// observability and debugging — the engine logs step transitions.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class WorkflowStepAttribute : Attribute
{
    public string Name { get; }
    public WorkflowStepAttribute(string name) => Name = name;
}
```

### Wait Types and Builders

```csharp
namespace ResumableWorkflows.Definition.Waits;

/// <summary>
/// Represents a point where the workflow pauses and waits for something.
/// Returned via yield return from the workflow's Execute() method.
/// </summary>
public class WorkflowWait
{
    public string Name { get; set; }
    public WorkflowWaitType Type { get; set; }
    public WorkflowMatchDescriptor MatchDescriptor { get; set; }
    public TimeSpan? Timeout { get; set; }
}

public enum WorkflowWaitType
{
    SignalWait,       // Wait for a signal from a service
    CommandWait,      // Wait for a command's response
    TimeWait,         // Wait for a duration or until a specific time
    GroupWaitAll,     // Wait for ALL of a set of waits
    GroupWaitAny      // Wait for ANY of a set of waits
}

/// <summary>
/// Serialized description of what the workflow is waiting for.
/// Stored in the database alongside the workflow state.
/// </summary>
public class WorkflowMatchDescriptor
{
    /// <summary>The signal or command name to match (e.g., "OrderApproved").</summary>
    public string TargetName { get; set; }

    /// <summary>Serialized match expression — evaluated against incoming messages.</summary>
    public string MatchExpressionJson { get; set; }

    /// <summary>Serialized action to run after a successful match.</summary>
    public string AfterMatchActionJson { get; set; }
}
```

### Wait Builder (Fluent API)

```csharp
namespace ResumableWorkflows.Definition.Waits;

/// <summary>
/// Fluent API for constructing waits inside a workflow. The workflow author
/// uses these methods to describe what to wait for and what to do when
/// the wait is satisfied.
/// </summary>
public interface IWorkflowWaitBuilder<TSignal>
{
    /// <summary>
    /// Define the condition for matching. Only signals/responses that
    /// satisfy this predicate will resume the workflow.
    /// Example: .MatchIf(s => s.OrderId == this.OrderId)
    /// </summary>
    IWorkflowWaitBuilder<TSignal> MatchIf(Expression<Func<TSignal, bool>> expression);

    /// <summary>
    /// Action to execute after a successful match, before the workflow
    /// continues. Typically used to capture data from the signal.
    /// Example: .AfterMatch(s => this.Result = s.Output)
    /// </summary>
    IWorkflowWaitBuilder<TSignal> AfterMatch(Expression<Action<TSignal>> action);

    /// <summary>
    /// Maximum time to wait. If exceeded, the wait completes with a timeout
    /// status and the workflow can handle it via branching.
    /// </summary>
    IWorkflowWaitBuilder<TSignal> WithTimeout(TimeSpan timeout);

    /// <summary>
    /// Human-readable name for this wait. Shown in the UI and logs.
    /// </summary>
    IWorkflowWaitBuilder<TSignal> Named(string name);

    /// <summary>Builds the final WorkflowWait object for yield return.</summary>
    WorkflowWait Build();
}

/// <summary>
/// Builder for group waits — waiting for multiple things at once.
/// </summary>
public interface IWorkflowGroupWaitBuilder
{
    IWorkflowGroupWaitBuilder Add(WorkflowWait wait);
    WorkflowWait WaitAll();
    WorkflowWait WaitAny();
}
```

### Generated Service Client (Base Class)

The source generator produces typed clients from `.workflow.json` files. These clients
extend the base class below, which provides the outbox-backed send mechanism.

```csharp
namespace ResumableWorkflows.Definition.Clients;

/// <summary>
/// Base class for generated service clients. Provides the outbox-backed
/// send mechanism. The engine calls through these clients to reach services.
///
/// The source generator produces subclasses like:
///   OrderServiceClient : WorkflowServiceClientBase
///     - WaitForOrderApproved() → returns a WorkflowWait for this signal
///     - SendCancelOrder(request) → writes command to outbox, returns WorkflowWait
/// </summary>
public abstract class WorkflowServiceClientBase
{
    /// <summary>The service ID this client targets.</summary>
    public abstract string TargetServiceId { get; }

    /// <summary>
    /// Sends a command through the outbox. The outbox guarantees delivery.
    /// Returns a WorkflowWait that the workflow yields to pause until
    /// the command is acknowledged or responded to.
    /// </summary>
    protected WorkflowWait SendCommand(string commandName, object payload);

    /// <summary>
    /// Creates a wait for a specific signal from this service.
    /// </summary>
    protected IWorkflowWaitBuilder<TSignal> WaitForSignal<TSignal>(string signalName);
}
```

---

## 7. ResumableWorkflows.Engine

The Engine is the heart of the system. It runs as a hosted service (standalone process
or embedded in an application). Its responsibilities:

- Load workflow DLLs and discover workflow definitions
- Create and track workflow instances
- Receive signals from services and match them to waiting workflows
- Send commands to services through the generated clients in workflow DLLs
- Manage workflow versions side-by-side
- Persist everything (instances, waits, state) to a pluggable store
- Provide diagnostics and admin APIs

### Engine Setup

```csharp
namespace ResumableWorkflows.Engine;

public static class WorkflowEngineExtensions
{
    /// <summary>
    /// Registers the workflow engine services: instance manager, wait matcher,
    /// executor, version manager, outbox/inbox, and all background workers.
    /// </summary>
    public static IServiceCollection AddWorkflowEngine(
        this IServiceCollection services,
        Action<WorkflowEngineOptions> configure);

    /// <summary>
    /// Maps the engine's endpoints:
    ///   POST /_workflow/receive    — single entry point for all signals from services
    ///   GET  /_workflow/health     — engine health check
    ///   GET  /_workflow/admin/*    — diagnostic and admin endpoints
    /// </summary>
    public static IApplicationBuilder UseWorkflowEngine(
        this IApplicationBuilder app);
}

public class WorkflowEngineOptions
{
    /// <summary>Unique ID for this engine instance (for clustering).</summary>
    public string EngineId { get; set; }

    /// <summary>Paths to workflow DLLs to load.</summary>
    public List<WorkflowDllConfig> WorkflowDlls { get; set; } = new();

    /// <summary>Registered services the engine communicates with.</summary>
    public List<WorkflowServiceInfo> Services { get; set; } = new();

    public WorkflowPersistenceOptions Persistence { get; set; } = new();
    public WorkflowVersioningOptions Versioning { get; set; } = new();
    public WorkflowOutboxOptions Outbox { get; set; } = new();
    public WorkflowInboxOptions Inbox { get; set; } = new();
    public WorkflowSecurityOptions Security { get; set; } = new();
}

public class WorkflowDllConfig
{
    public string Path { get; set; }
    public string WorkflowName { get; set; }
    public double Version { get; set; }
}
```

### Core Engine Interfaces

```csharp
namespace ResumableWorkflows.Engine.Core;

/// <summary>
/// The top-level orchestrator. Receives all external input and coordinates
/// the internal components. This is the main entry point for the engine.
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>
    /// Process an incoming signal from a service. The engine:
    ///   1. Deduplicates via inbox
    ///   2. Passes to WaitMatcher to find matching workflow instances
    ///   3. Resumes each matched instance via WorkflowExecutor
    /// </summary>
    Task ProcessIncomingSignal(WorkflowMessage message);

    /// <summary>
    /// Process a response to a command the engine previously sent.
    /// The engine matches it to the waiting workflow by InResponseToMessageId,
    /// which contains the original command's MessageId.
    /// </summary>
    Task ProcessIncomingResponse(WorkflowMessage message);

    /// <summary>Resume a specific workflow instance from a completed wait.</summary>
    Task ResumeWorkflow(string instanceId, WorkflowWait completedWait);

    /// <summary>Cancel a running workflow instance.</summary>
    Task CancelWorkflow(string instanceId, string reason);

    /// <summary>Get the current status of a workflow instance.</summary>
    Task<WorkflowInstanceInfo> GetInstanceStatus(string instanceId);
}
```

### Engine Receiver (Single Endpoint)

```csharp
namespace ResumableWorkflows.Engine.Receivers;

/// <summary>
/// The engine's single connection point for all incoming messages.
/// Services send signals here. The engine never exposes per-workflow
/// or per-signal endpoints — everything flows through one door.
///
///   POST /_workflow/receive
///
/// The receiver:
///   1. Validates HMAC signature
///   2. Checks inbox for duplicate message ID
///   3. Finds matching waits via IWorkflowWaitMatcher
///   4. Marks matched waits as SignalReceived (persisted — crash-safe)
///   5. Returns ACK to sender
///   6. Triggers async resume of matched workflow instances
///
/// If the engine crashes after step 4 but before step 6, restart
/// recovers by scanning for waits in SignalReceived state.
/// </summary>
internal interface IWorkflowEngineReceiver
{
    Task<WorkflowMessageResponse> Receive(WorkflowMessage message);
}
```

### Wait Matching

```csharp
namespace ResumableWorkflows.Engine.Matching;

/// <summary>
/// Finds workflow instances that are waiting for a given signal.
/// When a signal arrives, the matcher:
///   1. Looks up active waits by signal name
///   2. Evaluates each wait's match expression against the signal data
///   3. Returns all matching waits (one signal can resume multiple workflows)
/// </summary>
internal interface IWorkflowWaitMatcher
{
    Task<IReadOnlyList<WorkflowMatchedWait>> FindMatches(WorkflowMessage signal);
}

/// <summary>
/// Result of a successful match — links a specific wait to the signal that satisfied it.
/// </summary>
public class WorkflowMatchedWait
{
    public string WaitId { get; set; }
    public string InstanceId { get; set; }
    public string WorkflowUrn { get; set; }
    public double WorkflowVersion { get; set; }
    public WorkflowWait Wait { get; set; }
    public WorkflowMessage MatchedSignal { get; set; }
}
```

### Instance Management

```csharp
namespace ResumableWorkflows.Engine.Instances;

/// <summary>
/// Manages the lifecycle of workflow instances: creation, state persistence,
/// status transitions, and loading for execution.
/// </summary>
internal interface IWorkflowInstanceManager
{
    Task<string> CreateInstance(string workflowUrn, double version);
    Task<WorkflowInstance> Load(string instanceId);
    Task UpdateState(string instanceId, byte[] serializedState);
    Task SetStatus(string instanceId, WorkflowStatus status);
    Task<IReadOnlyList<WorkflowInstance>> GetActiveInstances(string workflowUrn);
}

public enum WorkflowStatus
{
    Created,
    Running,
    WaitingForSignal,
    WaitingForCommandResponse,
    WaitingForTime,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// State machine for individual waits. Instead of wrapping signal receive
/// and workflow resume in a single transaction, waits transition through
/// states. If the engine crashes between states, it recovers on restart
/// by scanning for waits in intermediate states.
///
///   Active → SignalReceived → Resuming → Completed
///                                ↘ Failed (if resume throws)
///
/// - Active:          Waiting for a matching signal or command response.
/// - SignalReceived:  A matching signal arrived. Persisted before attempting resume.
///                    If engine crashes here, restart picks it up and resumes.
/// - Resuming:        Workflow executor is running. Transient state (not persisted).
/// - Completed:       Workflow advanced past this wait.
/// - Failed:          Resume attempt threw an exception.
/// - Cancelled:       Wait was cancelled (timeout, workflow cancellation).
/// - TimedOut:        Wait exceeded its timeout duration.
/// </summary>
public enum WorkflowWaitStatus
{
    Active,
    SignalReceived,
    Resuming,
    Completed,
    Failed,
    Cancelled,
    TimedOut
}
```

### Workflow Execution

```csharp
namespace ResumableWorkflows.Engine.Execution;

/// <summary>
/// Executes workflow code. When a wait is satisfied, the executor:
///   1. Loads the workflow DLL (correct version via AssemblyLoadContext)
///   2. Deserializes the workflow instance state
///   3. Resumes Execute() from the point of the completed wait
///   4. Runs until the next yield return (new wait) or completion
///   5. Serializes and persists the updated state
///
/// If the workflow yields a SendCommand wait, the executor writes the
/// command to the engine's outbox for reliable delivery.
/// </summary>
internal interface IWorkflowExecutor
{
    Task<WorkflowExecutionResult> Execute(WorkflowInstance instance, WorkflowWait completedWait);
}

public class WorkflowExecutionResult
{
    public WorkflowStatus NewStatus { get; set; }
    public WorkflowWait? NextWait { get; set; }          // Null if completed
    public byte[] SerializedState { get; set; }
    public List<WorkflowMessage> OutgoingCommands { get; set; }  // Commands to send
    public string? Error { get; set; }
}
```

### Persistence

```csharp
namespace ResumableWorkflows.Engine.Persistence;

/// <summary>
/// Pluggable persistence interface. The engine stores everything through
/// this interface — swap the implementation to change the database.
///
/// Default: SQL Server. Also planned: SQLite.
/// </summary>
public interface IWorkflowEngineStore
{
    // ── Workflow Instances ──
    Task<WorkflowInstance> GetInstance(string instanceId);
    Task SaveInstance(WorkflowInstance instance);
    Task<IReadOnlyList<WorkflowInstance>> GetActiveInstances(string workflowUrn);

    // ── Waits ──
    Task SaveWait(WorkflowWaitRecord wait);
    Task<IReadOnlyList<WorkflowWaitRecord>> GetActiveWaits(string signalOrCommandName);
    Task CompleteWait(string waitId);
    Task CancelWait(string waitId, string reason);

    // ── Method/Signal Registry ──
    Task RegisterMethod(WorkflowMethodInfo method);
    Task<WorkflowMethodInfo?> GetMethod(string methodUrn);
    Task<IReadOnlyList<WorkflowMethodInfo>> GetMethodsByService(string serviceId);
    Task RemoveDeadMethods(string serviceId, string[] liveMethodUrns);

    // ── Signal Log ──
    Task LogSignal(WorkflowSignalRecord record);
    Task CleanupOldSignals(TimeSpan retention);
}
```

### Versioning

```csharp
namespace ResumableWorkflows.Engine.Versioning;

/// <summary>
/// Manages side-by-side workflow versions. When a new version of a workflow
/// DLL is deployed:
///   - Existing instances continue running on the old version
///   - New instances are created on the new version
///   - Old versions are kept alive until all their instances complete
///   - Dead versions (no active instances) are unloaded and cleaned up
///
/// Each version runs in its own AssemblyLoadContext for full isolation.
/// </summary>
public interface IWorkflowVersionManager
{
    Task RegisterVersion(string workflowUrn, double version, string assemblyPath);
    Task DeactivateVersion(string workflowUrn, double version, DateTime deactivationDate);
    Task<WorkflowActiveVersion> GetVersionForWait(string waitId);
    Task<IReadOnlyList<WorkflowActiveVersion>> GetActiveVersions(string workflowUrn);
    Task<bool> IsVersionDead(string workflowUrn, double version);
    Task CleanupDeadVersions();
}

/// <summary>
/// Loads and unloads workflow DLLs in isolated AssemblyLoadContexts.
/// This enables running v1.0 and v2.0 of the same workflow simultaneously
/// without type conflicts or shared state corruption.
/// </summary>
internal interface IWorkflowVersionIsolation
{
    Task<AssemblyLoadContext> LoadVersion(string workflowUrn, double version, string assemblyPath);
    Task UnloadVersion(string workflowUrn, double version);
}
```

### Service Registry

```csharp
namespace ResumableWorkflows.Engine.Registry;

/// <summary>
/// Tracks all registered services. The engine uses this to:
///   - Know where to send commands (each service's single endpoint URL)
///   - Validate that workflows reference services that actually exist
///   - Detect stale services that haven't sent a heartbeat
///   - Fetch and cache service definition files for compatibility checks
/// </summary>
public interface IWorkflowServiceRegistry
{
    Task Register(WorkflowServiceInfo info);
    Task Deregister(string serviceId);
    Task<WorkflowServiceInfo?> GetService(string serviceId);
    Task<IReadOnlyList<WorkflowServiceInfo>> GetServicesForSignal(string signalName);
    Task Heartbeat(string serviceId);
    Task<IReadOnlyList<WorkflowServiceInfo>> GetStaleServices(TimeSpan timeout);

    /// <summary>
    /// Fetches the .workflow.json from a service's definition endpoint
    /// and caches it. Used for compatibility validation.
    /// </summary>
    Task<string> FetchDefinition(string serviceId);
}
```

### Time-Based Scheduling

```csharp
namespace ResumableWorkflows.Engine.Scheduling;

/// <summary>
/// Abstraction over time-based wait scheduling. When a workflow yields a
/// TimeWait, the engine schedules a callback through this interface.
/// Default implementation: Hangfire.
/// </summary>
public interface IWorkflowTimeScheduler
{
    Task<string> ScheduleDelay(string instanceId, string waitId, TimeSpan delay);
    Task<string> ScheduleAt(string instanceId, string waitId, DateTime at);
    Task Cancel(string jobId);
}
```

### Diagnostics and Admin

```csharp
namespace ResumableWorkflows.Engine.Diagnostics;

/// <summary>
/// Admin and diagnostic interface. Powers the engine's UI and health endpoints.
/// </summary>
public interface IWorkflowEngineDiagnostics
{
    // ── Health ──
    Task<WorkflowEngineHealthReport> GetHealth();

    // ── Messaging ──
    Task<WorkflowOutboxStats> GetOutboxStats();
    Task<WorkflowInboxStats> GetInboxStats();
    Task<IReadOnlyList<WorkflowFailedMessage>> GetFailedMessages(int page, int pageSize);
    Task RetryFailedMessage(string messageId);

    // ── Instances ──
    Task<IReadOnlyList<WorkflowStuckInstance>> GetStuckWorkflows(TimeSpan waitingLongerThan);
    Task<WorkflowInstanceDetail> InspectInstance(string instanceId);
    Task<IReadOnlyList<WorkflowInstanceSummary>> ListInstances(
        string? workflowUrn, WorkflowStatus? status, int page, int pageSize);

    // ── Versions ──
    Task<IReadOnlyList<WorkflowActiveVersion>> GetActiveVersions(string? workflowUrn);
    Task<IReadOnlyList<WorkflowDeadVersion>> GetDeadVersions();

    // ── Services ──
    Task<IReadOnlyList<WorkflowServiceInfo>> GetRegisteredServices();
    Task<IReadOnlyList<WorkflowServiceInfo>> GetStaleServices();
}
```

---

## 8. The Definition File (workflow.json)

The `.workflow.json` file is the contract between a service and the workflows that
interact with it. It is analogous to an OpenAPI/Swagger spec — but for workflow
signals and commands instead of HTTP endpoints.

### Format

```json
{
  "$schema": "https://resumableworkflows.dev/schema/v1/service.json",
  "schemaVersion": "1.0",
  "serviceId": "order-service",
  "generatedAt": "2026-02-14T12:00:00Z",
  "generatedFrom": "MyApp.OrderService",

  "signals": [
    {
      "name": "OrderApproved",
      "methodName": "ApproveOrder",
      "description": "Fired when an order is approved by a user",
      "input": {
        "typeName": "ApproveOrderRequest",
        "properties": [
          { "name": "OrderId", "type": "Guid" },
          { "name": "ApprovedBy", "type": "string" }
        ]
      },
      "output": {
        "typeName": "OrderResult",
        "properties": [
          { "name": "Success", "type": "bool" },
          { "name": "OrderId", "type": "Guid" }
        ]
      }
    }
  ],

  "commands": [
    {
      "name": "CancelOrder",
      "methodName": "CancelOrder",
      "description": "Engine instructs this service to cancel an order",
      "input": {
        "typeName": "CancelOrderRequest",
        "properties": [
          { "name": "OrderId", "type": "Guid" },
          { "name": "Reason", "type": "string" }
        ]
      },
      "output": null
    }
  ],

  "signalAndCommand": [
    {
      "name": "ReserveInventory",
      "methodName": "ReserveInventory",
      "description": "Can be triggered by user (signal) or by engine (command)",
      "input": {
        "typeName": "ReserveRequest",
        "properties": [
          { "name": "OrderId", "type": "Guid" },
          { "name": "Items", "type": "List<string>" }
        ]
      },
      "output": {
        "typeName": "ReserveResult",
        "properties": [
          { "name": "Reserved", "type": "bool" },
          { "name": "WarehouseId", "type": "string" }
        ]
      }
    }
  ]
}
```

### How it travels

```
Service (build time)                    Workflow DLL (build time)
─────────────────────                   ────────────────────────
[WorkflowSignal] methods                .workflow.json file included
        │                                       │
   Client source gen                    Definition source gen
        │                                       │
        ▼                                       ▼
OrderService.workflow.json    ──copy──▶  Generated types:
(output in build artifacts)              - ApproveOrderRequest
                                         - OrderResult
                                         - OrderServiceClient
                                            .WaitForOrderApproved()
                                            .SendCancelOrder()
```

### Delivery methods

| Method | When | How |
|--------|------|-----|
| **File reference** | Monorepo, same solution | `<AdditionalFiles Include="..\..\services\OrderService.workflow.json" />` |
| **NuGet package** | Multi-repo, separate teams | Service publishes `OrderService.Workflow.Definition` NuGet containing the JSON |
| **Auto-fetch at build** | CI/CD with service discovery | `dotnet workflow fetch --from https://order-service/_workflow/definition` |
| **Runtime fetch** | Engine startup validation | Engine calls `GET /_workflow/definition` on each registered service |

---

## 9. Code Generation Pipeline

Two source generators work in two different projects, connected only by the JSON file.

### Generator 1: Client Source Generator (runs in the service project)

**Trigger:** finds methods with `[WorkflowSignal]` or `[WorkflowCommand]`.

**Produces:**

| Output | Purpose |
|--------|---------|
| `{ServiceName}.workflow.json` | The definition file — the service's contract |
| `WorkflowReceiverEndpoint.g.cs` | The single `POST /_workflow/receive` controller |
| `WorkflowDefinitionEndpoint.g.cs` | The `GET /_workflow/definition` controller |
| `WorkflowMessageRouter.g.cs` | Internal routing table: endpoint name → local method |
| `WorkflowSignalInterceptors.g.cs` | Method interceptors that capture signal input/output |

### Generator 2: Definition Source Generator (runs in the workflow project)

**Trigger:** finds `.workflow.json` files in `AdditionalFiles`.

**Produces:**

| Output | Purpose |
|--------|---------|
| `{ServiceId}Client.g.cs` | Typed service client extending `WorkflowServiceClientBase` |
| `{ServiceId}Types.g.cs` | C# record types for all input/output shapes defined in JSON |

**Example generated client:**

```csharp
// AUTO-GENERATED from order-service.workflow.json
// Do not edit.

public class OrderServiceClient : WorkflowServiceClientBase
{
    public override string TargetServiceId => "order-service";

    /// <summary>Wait for the OrderApproved signal.</summary>
    public IWorkflowWaitBuilder<OrderApprovedSignal> WaitForOrderApproved()
        => WaitForSignal<OrderApprovedSignal>("OrderApproved");

    /// <summary>Send the CancelOrder command via outbox.</summary>
    public WorkflowWait SendCancelOrder(CancelOrderRequest request)
        => SendCommand("CancelOrder", request);

    /// <summary>Wait for or send ReserveInventory.</summary>
    public IWorkflowWaitBuilder<ReserveInventorySignal> WaitForReserveInventory()
        => WaitForSignal<ReserveInventorySignal>("ReserveInventory");

    public WorkflowWait SendReserveInventory(ReserveRequest request)
        => SendCommand("ReserveInventory", request);
}

// Generated types
public record ApproveOrderRequest(Guid OrderId, string ApprovedBy);
public record OrderResult(bool Success, Guid OrderId);
public record OrderApprovedSignal(ApproveOrderRequest Input, OrderResult Output);
public record CancelOrderRequest(Guid OrderId, string Reason);
public record ReserveRequest(Guid OrderId, List<string> Items);
public record ReserveResult(bool Reserved, string WarehouseId);
public record ReserveInventorySignal(ReserveRequest Input, ReserveResult Output);
```

---

## 10. Communication Model — Single Endpoint

Both the Engine and each Service expose exactly **one endpoint** for receiving messages.
This is a deliberate design choice.

### Why one endpoint?

1. **Simplicity** — The engine doesn't need to know the URL structure of each service.
   It knows one URL per service and sends everything there.
2. **Security** — One endpoint to secure with HMAC, one endpoint to firewall.
3. **Protocol agnostic** — Whether it's HTTP, gRPC, or named pipe, there's one
   connection point. Swapping protocols doesn't change the routing logic.
4. **Service discovery** — Registering with the engine means sharing one URL, not a
   catalog of endpoints.

### The endpoints

**On every service (generated by Client):**

```
POST /_workflow/receive      Accepts WorkflowMessage, routes internally
GET  /_workflow/definition   Returns the .workflow.json file
```

**On the engine:**

```
POST /_workflow/receive      Accepts WorkflowMessage (signals from services)
GET  /_workflow/health       Engine health status
GET  /_workflow/admin/*      Diagnostic/admin UI endpoints
```

### Message routing

When a message arrives at `POST /_workflow/receive`, the `Endpoint` field in the
message body determines which method handles it:

```
Incoming WorkflowMessage:
{
    "messageId": "abc-123",
    "endpoint": "CancelOrder",     ← this determines routing
    "payload": "{...}",
    ...
}

Internal routing (generated):
    "CancelOrder"       → OrderService.CancelOrder(...)
    "ReserveInventory"  → InventoryService.ReserveInventory(...)
    "SendInvoice"       → BillingService.SendInvoice(...)
```

The developer never writes this routing code. The source generator builds the lookup
table from the decorated methods at compile time.

---

## 11. Signals and Commands

There is no "PushCall" concept. All communication uses two primitives:

### WorkflowSignal — "Something happened"

**Direction:** Service → Engine

When a method decorated with `[WorkflowSignal]` executes in a service, the Client
infrastructure captures its input and output, wraps them in a `WorkflowMessage`, and
sends it to the engine. The engine's `IWorkflowWaitMatcher` checks if any workflow
instances are waiting for this signal.

**The signal carries both input and output** because a workflow's match expression may
need to inspect either. For example, a workflow might wait for an order approval where
`output.Success == true` and `input.OrderId == this.OrderId`.

```
Service method executes → interceptor captures input + output
    → wraps in WorkflowMessage (type: Signal)
    → writes to outbox
    → outbox dispatcher sends to engine's /_workflow/receive
    → engine matches to waiting workflows
    → matched workflows resume
```

### WorkflowCommand — "Do something"

**Direction:** Engine → Service

When a workflow yields `SendCommand(...)`, the engine writes a command message to its
outbox. The outbox dispatcher sends it to the target service's `/_workflow/receive`
endpoint. The service's internal router dispatches it to the correct method.

```
Workflow yields SendCommand → engine writes to outbox
    → outbox dispatcher sends to service's /_workflow/receive
    → service router dispatches to the decorated method
    → method executes
    → response returned to engine
    → workflow resumes with the response
```

### A method can be both

When a method has both `[WorkflowSignal]` and `[WorkflowCommand]`:

- As a **signal**: the method is called normally by the service's own code (e.g., a user
  action). The interceptor captures input + output and sends the signal to the engine.
- As a **command**: the engine sends a command message telling the service to execute this
  method. The service executes it and returns the result.

In both cases, the same method runs. The difference is who initiates it.

---

## 12. Reliability — Outbox/Inbox

Both sides (Client and Engine) use the same outbox/inbox pattern for reliable messaging.
The implementation lives in a separate internal package: **`ResumableWorkflows.Reliability`**.

`ResumableWorkflows.Abstractions` stays pure — only data shapes and enums, no logic,
no SQLite, no background workers. The Reliability package contains the actual outbox
dispatcher, inbox store, SQLite access, retry logic, and cleanup jobs. Both Client and
Engine reference it as an internal dependency.

```
ResumableWorkflows.Abstractions   (contracts only — zero logic)
        ▲               ▲
        │               │
ResumableWorkflows.Reliability    (outbox/inbox implementation — SQLite, retry, cleanup)
        ▲               ▲
        │               │
  RF.Client        RF.Engine      (both use Reliability internally)
```

### Outbox (Sender Side)

Every outgoing message is first written to a local SQLite database in the same
transaction as the business operation. A background dispatcher polls for pending
messages and sends them. If sending fails, it retries with exponential backoff.

### Inbox (Receiver Side)

Every incoming message is checked against the inbox table by message ID. If already
processed, the cached response is returned without re-executing the handler. If new,
the handler executes and the inbox record is written in the same transaction.

### Guarantee

If the business operation committed, the message will eventually be delivered. If the
receiver processed the message, it will never process it again. This gives
**at-least-once delivery with exactly-once processing** — without any external
message broker.

### What lives where

| Component | Package |
|-----------|---------|
| `WorkflowMessage`, `WorkflowMessageResponse` | Abstractions |
| `WorkflowOutboxOptions`, `WorkflowInboxOptions` | Abstractions |
| `IWorkflowOutboxStore`, `IWorkflowInboxStore` (interfaces) | Reliability |
| SQLite outbox/inbox implementation | Reliability |
| `IWorkflowOutboxDispatcher` (background worker) | Reliability |
| `IWorkflowTransport` (pluggable send) | Reliability |
| Retry policy, exponential backoff | Reliability |
| Cleanup/retention jobs | Reliability |

---

## 13. Versioning

When a new version of a workflow DLL is deployed:

1. The engine loads the new DLL in a **separate AssemblyLoadContext**.
2. **Existing instances** continue running on the old version's code.
3. **New instances** are created using the new version.
4. The old version stays loaded until all its instances complete or are cancelled.
5. Once a version has zero active instances, it is marked as dead and unloaded.

Each version has a publish date and a deactivation date. Waits created between those
dates belong to that version. The engine routes incoming signals to the correct version
based on which version owns the waiting instance.

---

## 14. Deployment Scenarios

### Scenario A: Distributed (HTTP/gRPC)

The most common case. Services run as separate processes or containers. Communication
over HTTP or gRPC.

```
┌────────────┐     HTTP      ┌────────────────┐     HTTP      ┌────────────┐
│ OrderService│ ──signals──▶  │ Workflow Engine │ ──commands──▶ │EmailService│
│  (Web API)  │ ◀─commands── │   (Standalone)  │ ◀─signals─── │  (Web API)  │
└────────────┘               └────────────────┘               └────────────┘
                                     │
                              loads at runtime
                                     │
                              ┌──────▼──────┐
                              │ Workflow DLL │
                              └─────────────┘
```

### Scenario B: Same Machine (Named Pipes)

Services and engine on the same machine. Named pipes for fast local communication.

```
┌──────────────────────────────────────────────────┐
│                  Same Machine                     │
│                                                   │
│  ┌────────────┐  named pipe  ┌────────────────┐  │
│  │ OrderService│ ◀─────────▶ │ Workflow Engine │  │
│  └────────────┘              └────────────────┘  │
└──────────────────────────────────────────────────┘
```

### Scenario C: Same Process / DLL (In-Process)

The service code and workflow are in the same DLL. The engine loads the DLL and
communicates via in-process method calls through a serialization boundary.

```
┌─────────────────────────────────────────────┐
│              Workflow Engine Process          │
│                                              │
│  ┌──────────────────────────────────────┐   │
│  │  AssemblyLoadContext (isolated)       │   │
│  │  ┌────────────┐  ┌───────────────┐  │   │
│  │  │ Workflow DLL│  │ Service Code  │  │   │
│  │  │             │──│ (same DLL)    │  │   │
│  │  └────────────┘  └───────────────┘  │   │
│  └──────────────────────────────────────┘   │
│                                              │
│  InProcessTransport: direct method call      │
│  through serialization boundary              │
└─────────────────────────────────────────────┘
```

The engine uses `IWorkflowTransport` with an `InProcessTransport` implementation that
calls the service methods directly within the loaded AssemblyLoadContext. Serialization
at the boundary maintains state isolation.

---

## Dependency Graph Summary

```
ResumableWorkflows.Abstractions (zero external deps, zero logic)
  ├── Messages:       WorkflowMessage, WorkflowMessageResponse, WorkflowMessageHeaders
  ├── Configuration:  WorkflowOutboxOptions, WorkflowInboxOptions, WorkflowSecurityOptions
  └── Registration:   WorkflowServiceInfo

ResumableWorkflows.Reliability (depends on .Abstractions — internal, not published separately)
  ├── Interfaces:     IWorkflowOutboxStore, IWorkflowInboxStore, IWorkflowOutboxDispatcher
  ├── Implementation: SQLite outbox/inbox, retry logic, cleanup jobs
  └── Transport:      IWorkflowTransport (pluggable: HTTP, gRPC, Named Pipe, In-Process)

ResumableWorkflows.Client (depends on .Abstractions + .Reliability)
  ├── Attributes:     [WorkflowSignal], [WorkflowCommand]
  ├── Setup:          AddWorkflowClient(), UseWorkflowClient()
  ├── Source Gen:     Produces .workflow.json, receiver endpoint, definition endpoint, router
  └── Internal:       IWorkflowReceiver, IWorkflowMessageRouter, IWorkflowSignalInterceptor

ResumableWorkflows.Definition (depends on .Abstractions + .Reliability)
  ├── Base Classes:   WorkflowContainer, WorkflowServiceClientBase
  ├── Attributes:     [Workflow], [WorkflowStep]
  ├── Waits:          WorkflowWait, WorkflowWaitType, IWorkflowWaitBuilder, IWorkflowGroupWaitBuilder
  ├── Source Gen:     Reads .workflow.json → produces typed clients + types
  └── Match:          WorkflowMatchDescriptor

ResumableWorkflows.Engine (depends on .Abstractions + .Reliability + .Definition)
  ├── Setup:          AddWorkflowEngine(), UseWorkflowEngine()
  ├── Core:           IWorkflowEngine
  ├── Receiver:       IWorkflowEngineReceiver (single POST /_workflow/receive)
  ├── Matching:       IWorkflowWaitMatcher
  ├── Instances:      IWorkflowInstanceManager
  ├── Execution:      IWorkflowExecutor
  ├── Persistence:    IWorkflowEngineStore (pluggable)
  ├── Versioning:     IWorkflowVersionManager, IWorkflowVersionIsolation
  ├── Registry:       IWorkflowServiceRegistry (fetches definitions)
  ├── Scheduling:     IWorkflowTimeScheduler
  └── Diagnostics:    IWorkflowEngineDiagnostics

Developer's Workflow DLL (depends on .Definition)
  ├── Workflow classes extending WorkflowContainer
  ├── Copied .workflow.json files
  └── Generated service clients and types

Developer's Service (depends on .Client)
  ├── Business logic with [WorkflowSignal] / [WorkflowCommand]
  ├── Generated: single receiver endpoint, definition endpoint, router
  └── Outbox/Inbox SQLite databases
```