This document provides the finalized, comprehensive blueprint for the **Resumable Functions Ecosystem**. It defines the relationship between the Engine, the automated client services, and the "Workflows-as-Code" deployment model.

---

## 1. Ecosystem Overview

The ecosystem is a "Self-Configuring Distributed Operating System" for business logic. It eliminates manual infrastructure plumbing by using **Source Generators**, **Centralized Registries**, and **Dynamic Proxies**.

### The Three Pillars:

1. **The Workflow Engine:** The stateful orchestrator and artifact repository.
2. **The Federated Services:** External APIs that communicate via `Workflow.Client`.
3. **The Workflow Assemblies:** Specialized DLLs containing the C# resumable logic.

---

## 2. The Workflow Engine: The Source of Truth

The Engine is not just a runner; it is a **Database-First Management Hub**.

### A. Tracking & Persistence

* **Workflow Definitions:** Tracks all loaded DLLs, their versions, and their logical entry points.
* **Instance Store:** Manages the lifecycle of every active workflow, including its serialized variable state and its current "Wait" position.
* **Signal Log (Pushed Calls):** An immutable record of every incoming signal from services, ensuring exactly-once processing through deduplication.

### B. Execution Services

* **Matchmaker Engine:** Correlates incoming **Pushed Calls** with waiting **Workflow Instances** based on matching criteria (e.g., `OrderId`).
* **Assembly Load Manager:** Uses `AssemblyLoadContext` to load/unload workflow binaries at runtime without downtime.
* **Durable Timers:** A background poller that re-awakens workflows after a `Context.Delay` or an SLA timeout.

---

## 3. The Workflow Client: The "Simple Mechanism"

Integrated into every service via the `Workflow.Client` library, this module handles the "Last Mile" of reliability.

* **Shadow Controllers:** Automatically generated endpoints that the Engine calls to execute service logic.
* **SQLite Outbox/Inbox:** Ensures that if the network is down, signals are stored locally and retried automatically.
* **Metadata Export:** Exposes a `/_workflow/metadata` endpoint, allowing the Engine to "discover" available service methods.

---

## 4. Developer Experience (DX) & Deployment

The system removes the friction of manual referencing and file copying.

### A. Dynamic "Ghost" Proxies

Developers do not add project references or NuGet packages for other services.

1. The developer adds a **Service Link** (URL).
2. The **Source Generator** fetches metadata from the Engine.
3. It generates a **Typed Interface** in memory, providing instant IntelliSense for remote services.

### B. Zero-Touch Deployment

Workflows are deployed as versioned artifacts.

1. **Build:** Developer compiles the Workflow DLL.
2. **Push:** Using the `dotnet-wf` CLI, the DLL is POSTed to the Engine.
3. **Hot-Reload:** The Engine loads the assembly into an isolated context and begins executing new instances immediately.

---

## 5. Summary of System Interactions

| Operation | Trigger | Result |
| --- | --- | --- |
| **Registration** | Service Startup | Engine updates its Service Catalog and Metadata. |
| **Signaling** | Business Action | Service writes to SQLite Outbox  Pushes to Engine. |
| **Resumption** | Match Found | Engine locks state  Resumes C# method  Saves new state. |
| **Deployment** | `dotnet wf deploy` | Engine receives DLL  Hot-loads via `AssemblyLoadContext`. |

---

## 6. Enterprise Hardening (The "Foundation" Layer)

* **Distributed Locking:** Prevents race conditions when scaling the Engine horizontally.
* **Semantic Auditing:** Provides a "Time-Travel" view of every variable change in a workflow's history.
* **Quarantine & Rewind:** Allows developers to pause failed workflows, patch the data, and "rewind" to the last successful step.

---

**This documentation serves as the master guide for the implementation of the Resumable Functions project.**

**Would you like me to generate the SQL Schema for the Engine's "Matchmaking" tables (Instances, Definitions, and Calls) to finalize the data layer?**
