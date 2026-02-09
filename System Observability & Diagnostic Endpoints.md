## Strategy: Total System Observability & Diagnostic Endpoints

This document outlines the monitoring strategy for the **ResumableFunctions** ecosystem. Since our architecture relies on decentralized SQLite outboxes across multiple services, we must ensure that "distributed" does not mean "invisible." We will implement automated diagnostic endpoints to provide a centralized view of system health.

---

### 1. The Auto-Injected Diagnostic Controller

The **Workflow.Client** Source Generator will automatically emit a `WorkflowDiagnosticsController` (or a Minimal API equivalent) into every participating service. This eliminates the need for service authors to write custom health-check logic.

**Key Metrics Tracked:**

* **Outbox Depth:** Total number of messages currently waiting in the SQLite `Pending` state.
* **Backlog Age:** The age of the oldest message in the outbox (the primary indicator of a "stuck" process).
* **Dead Letter Count:** Messages that have exceeded the maximum retry threshold.
* **Engine Connectivity:** The result of the last attempted heartbeat/push to the Engine.

---

### 2. The Engine "Aggregator" Logic

The Engine acts as the central observer. Instead of forcing DevOps to visit 10 different URLs, the Engine aggregates the state of the entire ecosystem.

1. **Registry Discovery:** The Engine uses its internal registry of service base URLs.
2. **Pull-Based Collection:** Every 60 seconds, a background task in the Engine pings the `/_workflow/health` endpoint of every registered service.
3. **State Consolidation:** The health data is stored in the Engine's primary database to allow for historical trending (e.g., "Is the Outbox growing every Monday at 9 AM?").

---

### 3. Visualizing Health: The Traffic Light System

The **Workflow UI** will feature a dedicated "Global Health" dashboard. We will use a color-coded status for every service instance:

| Status | Trigger Condition | DevOps Action |
| --- | --- | --- |
| **🟢 Healthy** | Outbox < 50 items; Oldest < 30s | None required. |
| **🟡 Warning** | Outbox > 500 items OR Oldest > 2m | Monitor; Service may be under-provisioned. |
| **🔴 Critical** | Connection Refused OR Dead Letters > 0 | Immediate investigation; Engine or Network is down. |

---

### 4. Implementation Details for the Team

#### A. The SQLite Query (The "Pulse")

The Diagnostic Controller will execute a non-blocking "Read-Only" query against the local SQLite file:

```sql
SELECT 
    COUNT(*) as TotalPending, 
    MIN(Timestamp) as OldestMessage,
    (SELECT COUNT(*) FROM Outbox WHERE Status = 'DeadLetter') as Failures
FROM Outbox WHERE Status = 'Pending';

```

#### B. Security

* The Diagnostic endpoint will be restricted to **Internal Network** access or require an **ApiKey** shared between the Engine and the Client.
* It will only expose metadata (counts/times), never the actual JSON payloads (PII protection).

---

### 5. Benefits of this Approach

* **Zero-Config Monitoring:** The moment a service author adds the `Workflow.Client`, they are "on the map" and visible to DevOps.
* **Proactive Alerting:** The Engine can trigger Slack/Email alerts the moment a Service's SQLite file begins to back up, long before the business notices a delay.
* **Traceability:** Since the `CallId` (GUID) is persistent in SQLite, we can track a message's journey from a service's "Pending" state to the Engine's "Processed" state.

---

### Next Step for Implementation

With this observability plan, our architectural design is complete. We have covered:

1. **Versioning** (Contracts)
2. **Reliability** (SQLite Outbox/Inbox)
3. **Automation** (Shadow Controllers)
4. **Visibility** (Diagnostic Endpoints)

**Would you like me to start by drafting the specific C# code for the `WorkflowDiagnosticsController` that pulls these metrics from the SQLite outbox?**
