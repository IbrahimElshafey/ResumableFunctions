# Roadmap: Multi-Version Support & Workflow UI

This document outlines the strategic plan to evolve **ResumableFunctions** into a robust workflow engine with support for versioning, external service integration, and a visual designer.

## 1. Syntax & Attribute Refactoring

To better align with workflow terminology, the core attributes will be renamed:

* **Attribute Renaming:** * `[ResumableFunction]`  `[WorkflowStart]`
* `[SubResumableFunction]`  `[SubWorkflow]`


* **Explicit Versioning:** Workflows will now include a version parameter.
* *Example:* `[Workflow("NewEmpOnboarding", "1.0")]`



## 2. Core Architecture & Decoupling

* **Pure Core:** The workflow core DLL will have **zero external references** to ensure it remains lightweight and portable.
* **Service Compatibility:** * External services will send a version number to ensure compatibility.
* The engine will **automatically generate client interfaces** for services used within workflows.
* If a service version changes, a unique interface will be generated to prevent breaking changes.


* **Protocol Support:** Services can be implemented using **WebAPI**, **gRPC**, or other standard protocols.
* **Client Rebranding:** `ResumableFunctions.Client` will be renamed to `Workflow.Client`.

## 3. Workflow UI & Designer

The project will feature two distinct management interfaces:

1. **Tracking & Control (Existing):** For monitoring and managing active workflow instances (updates and enhancements pending).
2. **Workflow Designer (New):** A visual builder that outputs **JSON**. This JSON is then converted into C# code and an executable Workflow Function.

## 4. Development Approaches

Developers can choose the path that best fits their project requirements:

| Feature | **Code-First Approach** | **UI-Builder Approach** |
| --- | --- | --- |
| **Logic Management** | Manual C# implementation | Visual drag-and-drop |
| **Versioning** | Managed manually by the developer | Managed automatically by the engine |
| **Service References** | Manually defined | Handled by the internal registry |
| **Flexibility** | Maximum control over code | Rapid development & auto-generation |

### Registry & Participation

We will implement a **Central Registry** to track all participated versions of workflows and services, ensuring seamless routing between different versions of the same business logic.

-----
## Recommended Implementation Order

### Phase 1: Core Refactoring (The Foundation)
1. **Attribute & Naming Migration:** Rename the attributes to `WorkflowStart` and `SubWorkflow`. This is low-risk but sets the tone.
2. **Version Registry:** Implement the registry logic that can track multiple versions of the same workflow.
3. **Dependency Decoupling:** Strip the core DLL of external references. This is the hardest part but essential for the "Auto-Interface" generation you planned.

### Phase 2: The "Contract" Layer
1. **Service Interface Generator:** Build the logic that scans the external service version and generates the C# interface.
2. **Versioning Logic:** Ensure the engine can correctly route a message from a "Version 2" service to the correct "Version 2" workflow instance.

### Phase 3: The UI Designer (The Surface)
1. **JSON Schema Definition:** Define exactly what the UI needs to output to satisfy the Core engine you just built.
2. **Designer Build:** Create the drag-and-drop interface.
3. **The Generator:** Write the service that converts that JSON into the C# Workflow Function.

--------
# Strategic Plan: Multi-Version Support & Service Contracts

This document outlines the architectural shift to support multiple versions of workflows and external services within the **ResumableFunctions** (now **Workflow.Engine**) ecosystem. The strategy leverages **Source Generators**, **Embedded Resource Contracts**, and **Assembly Isolation** to ensure breaking changes in services do not disrupt long-running business processes.

---

## 1. The Versioned Contract Model

To avoid the brittleness of shared DLLs, we move to an **Interface-First** contract model. The service defines what it can do, and the workflow explicitly "opts-in" to a specific version of that capability.

### A. Service-Side (The Provider)

The **Workflow.Client** includes a Source Generator that monitors methods decorated with `[PushCall]`.

* **Automatic Interface Export:** The generator creates a C# interface file (e.g., `IEmpService_v1.cs`).
* **Embedded Resources:** This interface is not just compiled; it is injected as an **Embedded Resource** within the Service's DLL.
* **Discovery:** When the Service starts, it reports its available interface versions to the Engine Registry.

### B. Workflow-Side (The Consumer)

The workflow author defines compatibility by manually copying the generated interface into the Workflow project.

* **Explicit Linking:** By using `IEmpService_v1` instead of a generic `IEmpService`, the workflow is "pinned" to a specific contract version.
* **Zero Ambiguity:** The engine uses the interface's unique Type Name and Namespace to route calls, removing the need for runtime version headers in HTTP/gRPC requests.

---

## 2. Engine Architecture: The Switchboard

The Engine manages the lifecycle of these versions using a **Registry** and isolated **Load Contexts**.

### Discovery & Registration

When a Workflow DLL is uploaded, the Engine performs a "Manifest Scan":

1. **Metadata Extraction:** Reads a Source-Generated struct inside the DLL that lists all required interfaces and the workflow's own version (e.g., `Workflow("Onboarding", "2.0")`).
2. **Compatibility Check:** The Engine verifies that the Registry contains active Services providing those specific interfaces.
3. **Instance Mapping:** New workflow instances are started using the latest version, while existing "In-Flight" instances remain locked to the DLL version they started with.

### Assembly Isolation

To prevent dependency hell between different workflow versions:

* Each Workflow version is loaded into a unique `AssemblyLoadContext`.
* **Memory Management:** Once the Engine detects that a specific version has zero "In-Flight" instances and is marked as deprecated, it **unloads** the context to reclaim RAM.

---

## 3. Workflow UI & Designer Integration

The transition from visual design to executable code is managed via a JSON intermediate layer.

1. **UI-Builder:** The designer fetches available Service Interfaces from the Engine's Registry.
2. **JSON Schema:** The designer outputs a JSON representation of the workflow steps.
3. **The Generator:** A backend service converts this JSON into C# code, incorporating the correct versioned interfaces, and compiles it into a versioned Workflow DLL.

---

## 4. Implementation Roadmap

### Phase 1: Contract Automation

* Update `Workflow.Client` to include the **Source Generator** for `[PushCall]` interfaces.
* Implement the **Embedded Resource** logic to expose these interfaces to the developer.

### Phase 2: Registry & Routing

* Enhance the Engine Database to track `InterfaceName` + `ServiceVersion` + `Endpoint`.
* Develop the **Matchmaker** logic that routes incoming service calls to the correct `AssemblyLoadContext` based on the method signature.

### Phase 3: Lifecycle Management

* Implement the **Drain & Unload** logic for old versions.
* Build the dashboard view in the Workflow UI to visualize "In-Flight" counts per version.

---

**Next Step:** Would you like me to draft the **C# Source Generator logic** for the `Workflow.Client` that specifically handles the creation of these Embedded Resource interfaces?
####################################################
# Implementation Plan: Reliable Pushing via Local SQLite Outbox

This document details the architecture for ensuring **Exactly-Once Delivery** between external services and the **Workflow Engine** using a "Zero-Infrastructure" approach. By replacing a central broker with a local SQLite-backed Outbox, we eliminate Single Points of Failure (SPOF) and ensure message durability during network or engine downtime.

---

## 1. Architectural Overview

The system relies on two primary components: the **Outbox** (on the Service side) and the **Deduplicator** (on the Engine side). Communication is handled via standard **WebAPI/gRPC**, while reliability is managed at the database level.

### The "Reliability Loop"

1. **Capture:** The `Workflow.Client` intercepts a `[PushCall]` and persists it to a local SQLite file.
2. **Dispatch:** A background worker polls SQLite and attempts to POST the call to the Engine.
3. **Acknowledge:** The Engine processes the call and returns a success code.
4. **Cleanup:** The Service marks the call as completed or deletes it.

---

## 2. Service Side: The `Workflow.Client` Outbox

The Client library will now include a lightweight persistence layer and a background dispatcher.

### A. The SQLite Schema

A local `workflow_outbox.db` will be initialized automatically by the client:

| Column | Type | Description |
| --- | --- | --- |
| **CallId** | GUID | Primary Key, unique identifier for the specific call. |
| **MethodName** | String | The interface method being invoked. |
| **Payload** | JSON/Blob | The serialized arguments for the call. |
| **Status** | Enum | `Pending`, `InFlight`, `Completed`. |
| **Retries** | Int | Number of failed attempts. |
| **ExpiresAt** | DateTime | TTL for the message to prevent infinite retries of dead calls. |

### B. The Dispatcher (Background Worker)

A `BackgroundService` (IHostedService) executes the following loop:

1. Query `Pending` calls from SQLite (ordered by timestamp).
2. Attempt to send to Engine via `HttpClient`/`gRPC`.
3. **On 2xx Success:** Delete the row or update status to `Completed`.
4. **On 5xx or Connection Error:** Increment `Retries` and apply an **Exponential Backoff** before the next attempt.
5. **On 4xx (Logic Error):** Move to a "Dead Letter" state for manual inspection; retrying will not fix a bad request.

---

## 3. Engine Side: The Idempotent Inbox

The Engine must be "Idempotent aware" to handle cases where the Service resends a call because the original ACK was lost.

### A. Deduplication Table

The Engine maintains a `ProcessedCalls` table (in its primary SQL/NoSQL store):

* **MessageId (GUID)**
* **ProcessedDate (DateTime)**

### B. The Processing Logic

When the Engine receives a request:

1. **Transaction Start:** Open a DB transaction.
2. **Check:** Query `ProcessedCalls` for the incoming `CallId`.
3. **Action:** * **If Found:** Immediately return `200 OK` (the "Duplicate ACK").
* **If New:** * Execute the `ResumableFunction` logic.
* Insert the `CallId` into `ProcessedCalls`.




4. **Commit:** Finalize the transaction. If the transaction fails, no ACK is sent, and the Service will safely retry.

---

## 4. Key Advantages

* **No SPOF:** There is no central RabbitMQ or Bus to go down. If the Engine is offline, every Service independently queues its own data.
* **Exactly-Once Guarantee:** The combination of the **Outbox** (ensuring the message eventually leaves) and the **Deduplication Table** (ensuring it is only executed once) fulfills the exactly-once requirement.
* **Performance:** SQLite is extremely fast for local writes. The "heavy" work of network communication happens in the background, so the Service's main thread remains non-blocking.
* **Survivability:** The system survives Service restarts (SQLite is persistent) and Engine restarts (Engine recovers its processed list).

---

## 5. Development Next Steps

### Phase 1: Client Enhancement

* Implement the SQLite initialization logic within `Workflow.Client`.
* Develop the `OutboxDispatcher` background service with exponential backoff.

### Phase 2: Engine Middleware

* Add the `ProcessedCalls` table to the Engine schema.
* Create an `IdempotencyFilter` or Middleware to automatically wrap incoming `[PushCall]` endpoints.

### Phase 3: Monitoring

* Update the Workflow UI to show "Pending Outbox Counts" across all registered services.
#################################################

# Plan: Automated Reliable Messaging via Shadow Controllers

This document outlines the strategy for **Exactly-Once Bidirectional Communication** between the **Workflow Engine** and **External Services**. We eliminate the need for manual plumbing by using **Source Generators** to produce "Shadow Controllers" that handle persistence and deduplication automatically.

## 1. The Core Philosophy

Developers should focus only on business logic. The `Workflow.Client` library, powered by Source Generators, will wrap the author's code in a "Reliability Envelope" that uses a local SQLite database to guarantee delivery and prevent duplicate executions.

---

## 2. Service-to-Engine Path (The Outbox)

### Developer Experience

The author simply calls a method on a generated proxy.

```csharp
// Author's code
await _orderService.PushOrderUpdate(myOrder); 

```

### Behind the Scenes (Automatic)

* **Shadow Proxy:** The Source Generator produces an implementation of `IOrderService` that doesn't call the Engine directly.
* **Local Persistence:** The proxy saves the call data and a unique `CallId` into the local **SQLite Outbox**.
* **Background Dispatcher:** A hidden `IHostedService` in the library polls SQLite and POSTs the data to the Engine. It handles retries and exponential backoff automatically.

---

## 3. Engine-to-Service Path (The Shadow Controller)

This is the "Reverse Path" where the Engine triggers logic inside a service.

### Developer Experience

The author defines a standard method with a `[ResumableFunctionEntryPoint]` (or similar) attribute. No HTTP or routing code is required.

### Behind the Scenes (The Shadow Controller)

For every class containing entry points, the Source Generator creates a **Shadow Controller**:

1. **Hidden Endpoint:** It registers a unique route (e.g., `/api/_workflow/inbox/{MethodId}`).
2. **Inbound Deduplication:** When a call arrives, the Shadow Controller extracts the `CommandId` and checks the local **SQLite Inbox** table.
3. **Idempotent Execution:** * If the `CommandId` exists, it returns `200 OK` immediately without re-running logic.
* If new, it calls the author’s method, records the success in SQLite, and returns `200 OK`.


4. **Error Handling:** If the author's code fails, the Shadow Controller returns a `5xx`, triggering the Engine's own retry logic.

---

## 4. Symmetry of Persistence (Bidirectional SQLite)

Both paths leverage the same local SQLite file, partitioned into two logical areas:

| Table | Direction | Purpose |
| --- | --- | --- |
| **Outbox** | Service  Engine | Stores outgoing calls to ensure they eventually reach the Engine. |
| **Inbox** | Engine  Service | Stores received `CommandIds` to ensure local logic only runs once. |

---

## 5. Summary of Automated Features

* **No Endpoint Mapping:** Authors don't call `MapControllers()` for workflow logic; the Shadow Controllers are injected via the library's Middleware.
* **Auto-Migrations:** The SQLite schema is self-healing and managed by the `Workflow.Client` on startup.
* **Protocol Agnostic:** While the Shadow Controllers use WebAPI/gRPC, the author only interacts with C# interfaces and POCOs.
* **Zero Infrastructure:** No external brokers (RabbitMQ/Bus) are required; reliability is local to the service and the engine.

---

## 6. Implementation Roadmap

1. **Generator Update:** Create the Source Generator logic to emit the **Shadow Controller** classes with the SQLite deduplication logic baked in.
2. **Middleware Injection:** Implement the `IStartupFilter` in `Workflow.Client` to automatically register these shadow routes.
3. **Engine Dispatcher:** Update the Engine to use an **Internal Outbox** for calling these shadow endpoints, ensuring it retries if a service is temporarily offline.

###################################################

