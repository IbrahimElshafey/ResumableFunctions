# Implementation Plan: Security, Retention, and Performance Optimization

This document outlines the final hardening layer for the **ResumableFunctions** engine. To move from a prototype to an enterprise-grade middleware, we must address data lifecycle management, endpoint security, and the "In-Memory" speed-layer.

---

## 1. SQLite Performance: The "Dual-Path" Fast-Track

To achieve near-zero latency, we will implement a **Memory-First, Disk-Parallel** strategy. This ensures that the disk I/O of SQLite does not block the main application thread.

### The Mechanism

* **The In-Memory Channel:** We utilize `System.Threading.Channels<T>` as a high-speed internal buffer.
* **The Fast Path:** When a `[PushCall]` is triggered, the message is instantly pushed to the channel. the `OutboxDispatcher` picks it up immediately for transmission to the Engine.
* **The Durable Path:** Simultaneously, a background task commits the message to SQLite.

> **Critical Guardrail:** The author's `Task` only completes once the **Durable Path** (SQLite) confirms the write. The "Fast Path" simply ensures the network call starts the millisecond the disk write is initiated.

---

## 2. Security: Protecting the Shadow Controllers

Because Shadow Controllers are auto-generated and exposed via HTTP/gRPC, they represent a new attack surface. We must ensure only the **Workflow Engine** can trigger these endpoints.

### Implementation: Shared Signing Secret

1. **Handshake on Registration:** When a Service first registers with the Engine, they exchange a **Symmetric Signing Key** (HMACSHA256).
2. **Request Signing:** The Engine signs every callback request with a timestamp and a signature in the header:
`X-Workflow-Signature: t=16254829,v1=sha256(key, payload)`
3. **Shadow Validation:** The auto-generated Shadow Controller includes a pre-execution check. If the signature is missing or invalid, it returns `401 Unauthorized` without ever touching the SQLite Inbox or the author's logic.

---

## 3. Retention Policy: The SQLite Pruning Engine

A "Simple Mechanism" becomes a liability if the local SQLite file grows indefinitely. We will implement an automated **Cleanup Task** within the `Workflow.Client`.

### Policy Defaults

* **Success Pruning:** Rows marked as `Completed` are deleted after **24 hours**.
* **Dead Letter Retention:** Rows that failed all retries are kept for **7 days** to allow for manual recovery/investigation, then archived or deleted.
* **Vacuuming:** The client executes a `VACUUM` command weekly (during low-traffic windows) to reclaim disk space and defragment the database file.

---

## 4. Addressing Version Bloat (Deprecation)

As services evolve, old **Shadow Controllers** and **Outbox Workers** for retired versions must be decommissioned to save RAM and CPU.

* **Remote Kill-Switch:** The Engine can send a `DECOMMISSION` command to a Service.
* **Automatic Unbinding:** Upon receiving this signal, the `Workflow.Client` stops the background workers for that specific version and unregisters the routes from the middleware, effectively "hibernating" the old code.

---

## 5. Risk Assessment & Mitigations

| Risk | Mitigation |
| --- | --- |
| **Disk Exhaustion** | Strict 24-hour retention policy + WAL mode to keep file size stable. |
| **Endpoint Spoofing** | HMAC Signature validation in every Shadow Controller. |
| **RAM Spikes** | Bounded `System.Threading.Channels` to prevent in-memory queue bloat. |
| **CI/CD Friction** | (Recommendation) Move from manual copy-paste to a shared Git Submodule for versioned interfaces. |

---

**Next Step for the Team:**
I recommend we start by coding the **In-Memory Signaling logic**. This is the core of the "Reliable-Fast" promise. **Would you like me to provide the C# implementation for the `DualPathDispatcher` using Bounded Channels?**
