# ResumableFunctions — Phase 2 Implementation Plan
## Workflow Versioning & Reliable Distributed Communication

**Author:** Ibrahim Elshafey  
**Date:** February 2026  
**Scope:** Extend the existing ResumableFunctions engine to support multi-version workflows and reliable service-to-engine communication without external brokers.

---

## 1. Ecosystem Architecture

The ecosystem is a self-configuring distributed operating system for business logic built on three pillars:

1. **The Workflow Engine** — Stateful orchestrator and artifact repository. Database-first management hub that tracks loaded DLLs, versions, workflow instance lifecycle (serialized state + wait position), and an immutable signal log for exactly-once processing.
2. **The Federated Services** — External APIs communicating via the `Workflow.Client` library. Each service independently manages its own reliability through local SQLite persistence.
3. **The Workflow Assemblies** — Versioned DLLs containing C# resumable logic, hot-loaded via `AssemblyLoadContext`.

### Engine Execution Services

- **Matchmaker Engine:** Correlates incoming pushed calls with waiting workflow instances based on matching criteria (e.g., `OrderId`).
- **Assembly Load Manager:** Uses `AssemblyLoadContext` to load/unload workflow binaries at runtime without downtime.
- **Durable Timers:** Background poller that re-awakens workflows after `Context.Delay` or SLA timeouts.

### Version Isolation Strategy

Old workflow instances continue running side-by-side with new versions via isolated `AssemblyLoadContext`. New instances use the latest deployed version. No state migration is required. Once all in-flight instances of a deprecated version complete, the context is unloaded to reclaim memory.

---

## 2. Versioned Contract Model

To avoid the brittleness of shared DLLs, the system uses an **Interface-First** contract model. Services define capabilities; workflows explicitly opt-in to a specific version.

### 2.1 Service-Side (The Provider)

The `Workflow.Client` Source Generator monitors methods decorated with `[PushCall]`:

1. **Automatic Interface Export:** Generates a C# interface file (e.g., `IEmpService_v1.cs`).
2. **Embedded Resources:** The interface is injected as an Embedded Resource within the service DLL.
3. **Discovery:** On startup, the service reports available interface versions to the Engine Registry.

### 2.2 Workflow-Side (The Consumer)

The workflow author references the generated interface (e.g., `IEmpService_v1`) to pin the workflow to that contract. The engine uses the interface's unique Type Name and Namespace to route calls — no runtime version headers needed.

### 2.3 Dynamic "Ghost" Proxies

Developers do not add project references or NuGet packages for other services:

1. Developer adds a **Service Link** (URL).
2. Source Generator fetches metadata from the Engine.
3. Generates a **Typed Interface** in memory with instant IntelliSense.

> **Offline Fallback:** The Source Generator must cache the last-fetched metadata locally so builds succeed when the Engine is unreachable.

### 2.4 Engine Discovery & Registration

When a Workflow DLL is uploaded, the Engine performs a **Manifest Scan**:

1. **Metadata Extraction:** Reads a Source-Generated struct listing all required interfaces and the workflow version.
2. **Compatibility Check:** Verifies the Registry contains active services providing those specific interfaces.
3. **Instance Mapping:** New instances start on the latest version; in-flight instances stay pinned to their original DLL.

### 2.5 Zero-Touch Deployment

1. **Build:** Developer compiles the Workflow DLL.
2. **Push:** Using the `dotnet-wf` CLI, the DLL is POSTed to the Engine.
3. **Hot-Reload:** The Engine loads the assembly into an isolated `AssemblyLoadContext` and starts executing new instances immediately.

---

## 3. Reliable Messaging: SQLite Outbox & Shadow Controllers

Exactly-once bidirectional communication between Engine and services. No external brokers (RabbitMQ/Kafka). Reliability is local to each service via SQLite + Source-Generated wrappers.

### 3.1 The Reliability Loop

1. **Capture:** `Workflow.Client` intercepts a `[PushCall]` and persists it to a local SQLite file.
2. **Dispatch:** Background worker POSTs the message to the Engine.
3. **Deduplicate:** Engine checks its `ProcessedCalls` table. If `CallId` exists → `200 OK`. If new → execute workflow logic within a transaction.
4. **Acknowledge:** On success, the service marks the Outbox row as `Completed`.

### 3.2 Service-to-Engine Path (The Outbox)

The author calls a method on a generated proxy. Behind the scenes:

- **Shadow Proxy** saves the call data + unique `CallId` into the local SQLite Outbox.
- **Background Dispatcher** (`IHostedService`) handles retries with exponential backoff.

#### Outbox Schema

| Column       | Type     | Description                                    |
|--------------|----------|------------------------------------------------|
| CallId       | GUID     | Primary key, unique identifier.                |
| MethodName   | String   | Interface method being invoked.                |
| Payload      | JSON/Blob| Serialized arguments.                          |
| Status       | Enum     | Pending, InFlight, Completed, DeadLetter.      |
| Retries      | Int      | Number of failed dispatch attempts.            |
| ExpiresAt    | DateTime | TTL to prevent infinite retries.               |

#### Dispatcher Behavior

- **2xx Success:** Mark row as Completed.
- **5xx / Connection Error:** Increment Retries, exponential backoff.
- **4xx (Logic Error):** Move to DeadLetter for manual inspection.
- **Safety Net:** Fallback poll every 30 seconds for missed signals during restarts.

### 3.3 Engine-to-Service Path (Shadow Controllers)

For every class with entry points, the Source Generator creates a **Shadow Controller**:

1. Registers a hidden endpoint: `/api/_workflow/inbox/{MethodId}`.
2. Extracts `CommandId`, checks local SQLite **Inbox** table for deduplication.
3. If new → executes author's method, records success. If duplicate → returns `200 OK` immediately.
4. On failure → returns `5xx`, triggering the Engine's own retry logic.

#### Bidirectional SQLite Persistence

| Table  | Direction          | Purpose                                      |
|--------|--------------------|----------------------------------------------|
| Outbox | Service → Engine   | Stores outgoing calls to ensure delivery.    |
| Inbox  | Engine → Service   | Stores received CommandIds for deduplication.|

### 3.4 Automated Features

- **No Endpoint Mapping:** Shadow Controllers injected via middleware. Authors never call `MapControllers()`.
- **Auto-Migrations:** SQLite schema is self-healing, managed by `Workflow.Client` on startup.
- **Protocol Agnostic:** Authors interact only with C# interfaces and POCOs.
- **Zero Infrastructure:** No external brokers required.

---

## 4. Performance & Scaling Strategy

### 4.1 SQLite Optimization

- **WAL Mode:** `PRAGMA journal_mode=WAL` — simultaneous reads and writes without file-level locking.
- **Synchronous Normal:** `PRAGMA synchronous=NORMAL` — speed boost while remaining crash-safe.
- **Connection Pooling:** Dedicated long-lived connections to avoid file handle overhead.

### 4.2 Dual-Path Fast-Track Dispatcher

Memory-first, disk-parallel strategy for near-zero latency:

- **Fast Path:** On `[PushCall]`, the message is instantly pushed to a `System.Threading.Channels<T>` buffer. The dispatcher picks it up immediately.
- **Durable Path:** Simultaneously, a background task commits the message to SQLite.

> **Critical Guardrail:** The caller's `Task` only completes once the Durable Path (SQLite) confirms the write. The Fast Path simply ensures the network call starts the millisecond the disk write is initiated.

### 4.3 Source-Generated Execution Speed

- **Static Mapping:** Shadow Controllers generated as compiled `switch` statement routing — no reflection.
- **Minimal Pipeline:** Registered as Minimal API endpoints, bypassing the MVC filter/action pipeline.

### 4.4 Scalability Metrics

| Scenario             | Performance                                           |
|----------------------|-------------------------------------------------------|
| Transaction Latency  | +2ms to 5ms per SQLite row write.                     |
| Throughput           | 500–2,000 calls/second per service instance (SSD).    |
| Engine Overhead      | <1ms indexed GUID check in deduplication table.       |

For extreme scale (>5,000 calls/sec), `Workflow.Client` can swap to In-Memory SQLite or Redis as the Outbox provider. Shadow Controller logic remains identical.

---

## 5. Security: Protecting Shadow Controllers

Shadow Controllers are auto-generated and exposed via HTTP/gRPC — a new attack surface. Only the Workflow Engine must be allowed to trigger them.

### 5.1 Shared Signing Secret (HMACSHA256)

1. **Handshake on Registration:** When a service registers with the Engine, they exchange a symmetric signing key.
2. **Request Signing:** The Engine signs every callback with a timestamp and signature:
   `X-Workflow-Signature: t=16254829,v1=sha256(key, payload)`
3. **Shadow Validation:** The auto-generated Shadow Controller validates the signature before touching the Inbox or author's logic. Invalid/missing signature → `401 Unauthorized`.

---

## 6. Retention Policy: SQLite Pruning

A local SQLite file that grows indefinitely becomes a liability. The `Workflow.Client` includes an automated cleanup task.

### 6.1 Policy Defaults

| Rule                 | Policy                                                  |
|----------------------|---------------------------------------------------------|
| Success Pruning      | Rows marked Completed deleted after **24 hours**.       |
| Dead Letter Retention| Failed rows kept **7 days** for investigation, then deleted. |
| Vacuuming            | `VACUUM` command weekly during low-traffic windows.     |

### 6.2 Version Deprecation

- **Remote Kill-Switch:** The Engine can send a `DECOMMISSION` command to a service.
- **Automatic Unbinding:** On receiving this signal, `Workflow.Client` stops background workers for that version and unregisters routes, effectively hibernating old code.

---

## 7. System Observability & Diagnostics

Since the architecture relies on decentralized SQLite outboxes, "distributed" must not mean "invisible."

### 7.1 Auto-Injected Diagnostic Controller

The Source Generator emits a `WorkflowDiagnosticsController` into every service automatically. Metrics tracked:

- **Outbox Depth:** Messages in `Pending` state.
- **Backlog Age:** Age of the oldest pending message.
- **Dead Letter Count:** Messages that exceeded max retries.
- **Engine Connectivity:** Result of the last heartbeat/push.

### 7.2 Engine Aggregator

The Engine acts as the central observer:

1. Uses its internal registry of service base URLs.
2. Every 60 seconds, pings `/_workflow/health` on every registered service.
3. Stores health data in the Engine DB for historical trending.

### 7.3 Traffic Light Dashboard

| Status      | Condition                                  | Action                        |
|-------------|--------------------------------------------|-------------------------------|
| 🟢 Healthy  | Outbox < 50 items; Oldest < 30s            | None required.                |
| 🟡 Warning  | Outbox > 500 items OR Oldest > 2m          | Monitor; may be under-provisioned. |
| 🔴 Critical | Connection Refused OR Dead Letters > 0     | Immediate investigation.      |

### 7.4 Security

- Diagnostic endpoint restricted to internal network or shared API key.
- Only exposes metadata (counts/times), never actual JSON payloads (PII protection).

---

## 8. Risk Assessment & Mitigations

| Risk                  | Mitigation                                                        |
|-----------------------|-------------------------------------------------------------------|
| Disk Exhaustion       | Strict 24-hour retention + WAL mode for stable file size.         |
| Endpoint Spoofing     | HMAC signature validation on every Shadow Controller.             |
| RAM Spikes            | Bounded `System.Threading.Channels` to prevent queue bloat.       |
| Signal Loss           | 30-second fallback polling as safety net.                         |
| Version Conflicts     | Interface-first contracts with explicit version pinning.          |
| Stale Ghost Proxies   | Source Generator caches metadata locally; warns on staleness.     |

---

## 9. System Interactions Summary

| Operation        | Trigger                | Result                                                       |
|------------------|------------------------|--------------------------------------------------------------|
| Registration     | Service Startup        | Engine updates Service Catalog and Metadata.                 |
| Signaling        | Business Action        | Service writes to SQLite Outbox → pushes to Engine.          |
| Resumption       | Match Found            | Engine locks state → resumes C# method → saves new state.   |
| Deployment       | `dotnet wf deploy`     | Engine receives DLL → hot-loads via `AssemblyLoadContext`.   |
| Decommission     | Engine Command         | Service hibernates old version workers and routes.           |
| Health Check     | Engine Poll (60s)      | Service reports Outbox depth, backlog age, dead letters.     |

---

## 10. Implementation Phases

### Phase 2.1 — Contract Automation
- Update `Workflow.Client` to include the Source Generator for `[PushCall]` interfaces.
- Implement Embedded Resource logic for interface export.
- Build metadata export endpoint (`/_workflow/metadata`).

### Phase 2.2 — Registry & Routing
- Enhance Engine DB to track `InterfaceName` + `ServiceVersion` + `Endpoint`.
- Develop Matchmaker routing based on method signature and version.
- Implement Manifest Scan for uploaded workflow DLLs.

### Phase 2.3 — SQLite Outbox & Shadow Controllers
- Implement SQLite Outbox initialization within `Workflow.Client`.
- Build the `OutboxDispatcher` background service with exponential backoff.
- Create the Source Generator for Shadow Controllers with Inbox deduplication.
- Implement `IStartupFilter` for automatic shadow route registration.

### Phase 2.4 — Performance Hardening
- Implement the Dual-Path dispatcher using `System.Threading.Channels<T>`.
- Configure SQLite WAL mode, synchronous=NORMAL, connection pooling.
- Register Shadow Controllers as Minimal API endpoints.

### Phase 2.5 — Security & Retention
- Implement HMAC signing handshake on service registration.
- Add signature validation to all Shadow Controllers.
- Build the automated SQLite pruning engine (24h success, 7d dead letter, weekly VACUUM).
- Implement the DECOMMISSION command for version deprecation.

### Phase 2.6 — Observability
- Source-generate the `WorkflowDiagnosticsController` into every service.
- Build the Engine health aggregator (60s polling cycle).
- Implement the traffic light dashboard in the Workflow UI.

---

## 11. Enterprise Hardening (Foundation Layer)

These capabilities exist in the current engine and will be extended for the distributed model:

- **Distributed Locking:** Prevents race conditions when scaling the Engine horizontally.
- **Semantic Auditing:** Time-travel view of every variable change in a workflow's history.
- **Quarantine & Rewind:** Pause failed workflows, patch data, rewind to last successful step.
- **Compensation Logic:** Cross-service rollback when downstream steps fail permanently.