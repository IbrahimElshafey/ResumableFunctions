# Reliable Messaging: SQLite Outbox/Inbox Pattern

## ResumableFunctions — Phase 2 Technical Specification

---

## 1. Problem Statement

When Service A sends a message (signal, command, or push call) to Service B (or the RF Engine), several failure modes can occur:

1. **Send failure** — the message never leaves Service A (network down, process crash before send).
2. **Receive failure** — the message reaches Service B but crashes before processing.
3. **ACK failure** — Service B processes the message successfully, but the acknowledgment never reaches Service A, causing Service A to believe the message was never delivered.
4. **Duplicate delivery** — Service A retries a message that was already processed by Service B.

Without a reliability layer, any of these failures results in either **lost messages** or **duplicate processing**, both of which corrupt workflow state.

### Design Goals

- **At-least-once delivery**: Every message eventually reaches its destination, even after crashes and restarts.
- **Exactly-once processing**: The receiver processes each logical message only once, even if delivered multiple times.
- **Zero-infrastructure**: No external message broker required. Uses local SQLite as the persistence layer.
- **Transparent to workflow authors**: Reliability is handled by the framework, not by the developer writing workflows.
- **Works across all communication protocols**: REST API, gRPC, Named Pipes, Unix Domain Sockets, in-process calls.

---

## 2. Architecture Overview

```
┌─────────────────────────────────────────────────────┐
│                     Service A (Sender)              │
│                                                     │
│  ┌──────────────┐    ┌──────────────────────────┐   │
│  │ Application  │───▶│     SQLite Outbox         │   │
│  │    Code      │    │  ┌──────────────────────┐ │   │
│  │              │    │  │ MessageId (PK)       │ │   │
│  │ signal/cmd/  │    │  │ Destination          │ │   │
│  │ pushcall     │    │  │ Payload              │ │   │
│  └──────────────┘    │  │ Status               │ │   │
│                      │  │ RetryCount           │ │   │
│                      │  │ NextRetryAt          │ │   │
│                      │  │ CreatedAt            │ │   │
│                      │  │ LastAttemptAt        │ │   │
│                      │  │ ExpiresAt            │ │   │
│                      │  └──────────────────────┘ │   │
│                      └────────────┬─────────────┘   │
│                                   │                  │
│  ┌────────────────────────────────▼──────────────┐  │
│  │          Outbox Dispatcher (Background)       │  │
│  │  - Polls for pending messages                 │  │
│  │  - Sends via configured transport             │  │
│  │  - Marks as Sent on ACK                       │  │
│  │  - Schedules retry on failure                 │  │
│  └───────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
                          │
                          │  HTTP / gRPC / Named Pipe / etc.
                          ▼
┌─────────────────────────────────────────────────────┐
│                    Service B (Receiver)              │
│                                                     │
│  ┌───────────────────────────────────────────────┐  │
│  │     Shadow Controller / Receiver Endpoint     │  │
│  │  - Receives incoming message                  │  │
│  │  - Checks Inbox for duplicate                 │  │
│  │  - If new: write to Inbox + process           │  │
│  │  - If duplicate: return previous result       │  │
│  └───────────────────┬───────────────────────────┘  │
│                      │                               │
│  ┌───────────────────▼───────────────────────────┐  │
│  │           SQLite Inbox                        │  │
│  │  ┌──────────────────────┐                     │  │
│  │  │ MessageId (PK)       │                     │  │
│  │  │ SourceServiceId      │                     │  │
│  │  │ ProcessedAt          │                     │  │
│  │  │ ResponsePayload      │                     │  │
│  │  │ ExpiresAt            │                     │  │
│  │  └──────────────────────┘                     │  │
│  └───────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
```

### Flow Summary

1. Application code calls a signal/command/push call.
2. Instead of sending directly, the message is **written to the local SQLite Outbox** within the same database transaction as the business operation.
3. The **Outbox Dispatcher** (a background worker) picks up pending messages and sends them via the configured transport.
4. The receiver's **Shadow Controller** checks the **Inbox** for duplicates before processing.
5. On success, the receiver writes the message ID to the Inbox and returns an ACK with an optional response payload.
6. The sender marks the Outbox entry as Sent.
7. On failure or no ACK, the sender retries according to the retry policy.

---

## 3. Outbox (Sender Side)

### 3.1 Schema

```sql
CREATE TABLE IF NOT EXISTS outbox_messages (
    message_id       TEXT PRIMARY KEY,           -- GUID, generated by sender
    correlation_id   TEXT,                        -- Links request/response pairs
    message_type     TEXT NOT NULL,               -- 'Signal' | 'Command' | 'PushCall'
    destination      TEXT NOT NULL,               -- Target service identifier or URL
    endpoint         TEXT NOT NULL,               -- Target endpoint / method name
    payload          TEXT NOT NULL,               -- JSON-serialized message body
    headers          TEXT,                        -- JSON-serialized metadata/headers
    status           TEXT NOT NULL DEFAULT 'Pending',  -- Pending | Sending | Sent | Failed | Expired
    retry_count      INTEGER NOT NULL DEFAULT 0,
    max_retries      INTEGER NOT NULL DEFAULT 5,
    next_retry_at    TEXT,                        -- ISO 8601 datetime
    last_attempt_at  TEXT,                        -- ISO 8601 datetime
    last_error       TEXT,                        -- Last failure reason
    created_at       TEXT NOT NULL DEFAULT (datetime('now')),
    expires_at       TEXT,                        -- Message TTL
    sent_at          TEXT                         -- When successfully acknowledged
);

CREATE INDEX idx_outbox_status_retry ON outbox_messages (status, next_retry_at)
    WHERE status IN ('Pending', 'Sending', 'Failed');
```

### 3.2 Writing to the Outbox

The critical guarantee: the outbox write happens **in the same transaction** as the business operation that produces the message.

```csharp
// Source-generated wrapper (conceptual)
public async Task SendSignal(string destination, string endpoint, object payload)
{
    var message = new OutboxMessage
    {
        MessageId = Guid.NewGuid().ToString(),
        MessageType = "Signal",
        Destination = destination,
        Endpoint = endpoint,
        Payload = JsonSerializer.Serialize(payload),
        Status = "Pending",
        MaxRetries = _options.DefaultMaxRetries,
        ExpiresAt = DateTime.UtcNow.Add(_options.DefaultMessageTTL)
    };

    // Written to SQLite in the SAME transaction as the caller's operation
    await _outboxStore.Insert(message);
}
```

### 3.3 Outbox Dispatcher

A background worker that polls for messages ready to be sent.

```csharp
// Pseudocode for the dispatcher loop
public class OutboxDispatcher : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var messages = await _outboxStore.GetPendingMessages(
                batchSize: 50,
                olderThan: DateTime.UtcNow
            );

            foreach (var msg in messages)
            {
                await _outboxStore.UpdateStatus(msg.MessageId, "Sending");

                try
                {
                    var response = await _transport.Send(msg);

                    if (response.Acknowledged)
                    {
                        await _outboxStore.MarkAsSent(msg.MessageId, DateTime.UtcNow);
                    }
                    else if (response.DuplicateDetected)
                    {
                        // Receiver already processed this — treat as success
                        await _outboxStore.MarkAsSent(msg.MessageId, DateTime.UtcNow);
                    }
                }
                catch (Exception ex)
                {
                    await _outboxStore.RecordFailure(msg.MessageId, ex.Message);
                    await _outboxStore.ScheduleRetry(msg.MessageId, CalculateNextRetry(msg));
                }
            }

            await Task.Delay(_options.PollingInterval, ct);
        }
    }
}
```

### 3.4 Retry Policy

Exponential backoff with jitter to prevent thundering herd:

```
Retry 1:  2 seconds  + random(0–500ms)
Retry 2:  4 seconds  + random(0–500ms)
Retry 3:  8 seconds  + random(0–500ms)
Retry 4:  16 seconds + random(0–500ms)
Retry 5:  32 seconds + random(0–500ms)
```

Formula: `delay = min(baseDelay * 2^retryCount, maxDelay) + random(0, jitter)`

After max retries, the message status transitions to `Failed`. A failed message can be:
- Manually retried via the admin UI.
- Picked up by a dead-letter processor.
- Expired if past its TTL.

### 3.5 Configuration

```csharp
public class OutboxOptions
{
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);
    public int DefaultMaxRetries { get; set; } = 5;
    public TimeSpan DefaultMessageTTL { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromSeconds(2);
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan JitterMax { get; set; } = TimeSpan.FromMilliseconds(500);
    public int BatchSize { get; set; } = 50;
    public string DatabasePath { get; set; } = "outbox.db";
}
```

---

## 4. Inbox (Receiver Side)

### 4.1 Schema

```sql
CREATE TABLE IF NOT EXISTS inbox_messages (
    message_id        TEXT PRIMARY KEY,            -- Same GUID from the sender
    source_service_id TEXT NOT NULL,               -- Who sent it
    endpoint          TEXT NOT NULL,               -- Which endpoint was called
    processed_at      TEXT NOT NULL DEFAULT (datetime('now')),
    response_payload  TEXT,                        -- Cached response for duplicate requests
    expires_at        TEXT                         -- When this record can be cleaned up
);

CREATE INDEX idx_inbox_expires ON inbox_messages (expires_at)
    WHERE expires_at IS NOT NULL;
```

### 4.2 Shadow Controller / Receiver Endpoint

The Shadow Controller is a source-generated endpoint that wraps the actual application endpoint with inbox deduplication logic. The developer never writes this code — the framework generates it.

```csharp
// Source-generated shadow controller (conceptual)
[HttpPost("/_rf/receive")]
public async Task<IActionResult> ReceiveMessage([FromBody] IncomingMessage message)
{
    // Step 1: Check inbox for duplicate
    var existing = await _inboxStore.Get(message.MessageId);

    if (existing != null)
    {
        // Already processed — return cached response
        return Ok(new MessageResponse
        {
            Acknowledged = true,
            DuplicateDetected = true,
            Payload = existing.ResponsePayload
        });
    }

    // Step 2: Process the message (within a transaction)
    using var transaction = await _db.BeginTransactionAsync();
    try
    {
        // Dispatch to actual handler
        var result = await _dispatcher.Handle(message.Endpoint, message.Payload);

        // Write to inbox (same transaction)
        await _inboxStore.Insert(new InboxRecord
        {
            MessageId = message.MessageId,
            SourceServiceId = message.SourceServiceId,
            Endpoint = message.Endpoint,
            ResponsePayload = JsonSerializer.Serialize(result),
            ExpiresAt = DateTime.UtcNow.Add(_options.InboxRetentionPeriod)
        });

        await transaction.CommitAsync();

        return Ok(new MessageResponse
        {
            Acknowledged = true,
            DuplicateDetected = false,
            Payload = result
        });
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        // Return error — sender will retry
        return StatusCode(500, new MessageResponse
        {
            Acknowledged = false,
            Error = ex.Message
        });
    }
}
```

### 4.3 Key Guarantee

The inbox write and the business logic execution happen **in the same SQLite transaction**. This means:

- If the process crashes **before commit**: neither the inbox record nor the business effect is persisted. The sender retries, and the message is processed for the first time.
- If the process crashes **after commit**: the inbox record exists. The sender retries, and the receiver returns the cached response without re-processing.
- If the ACK fails to reach the sender: the sender retries, the receiver finds the inbox record, and returns the cached response.

This is the core of exactly-once processing semantics over at-least-once delivery.

---

## 5. Message Contract

### 5.1 Wire Format

Every message on the wire carries these fields regardless of transport protocol:

```json
{
    "messageId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "correlationId": "x9y8z7w6-...",
    "sourceServiceId": "inventory-service",
    "messageType": "Signal",
    "endpoint": "OrderApproved",
    "payload": { ... },
    "headers": {
        "rf-version": "2.0",
        "rf-timestamp": "2026-02-14T12:00:00Z",
        "rf-hmac": "base64-encoded-signature"
    }
}
```

### 5.2 Message Types

| Type | Direction | Purpose |
|------|-----------|---------|
| **Signal** | Service → Engine | Notify the engine that something happened (e.g., form submitted, payment received). Engine matches it to waiting workflows. |
| **Command** | Engine → Service | Engine instructs a service to do something (e.g., send email, reserve inventory). |
| **PushCall** | Service → Engine | A method decorated with `[PushCall]` was invoked. The engine records it and matches to waits. |
| **Response** | Either direction | Reply to a Command or acknowledgment of a Signal. Carries `correlationId` linking it to the original. |

### 5.3 HMAC Security

Every message is signed using a shared secret between the sender and receiver.

```
HMAC-SHA256(
    key: shared_secret,
    data: messageId + sourceServiceId + endpoint + payload_hash
)
```

The receiver verifies the signature before processing. Invalid signatures are rejected immediately (HTTP 401) without writing to the inbox.

---

## 6. Retention & Cleanup

### 6.1 Outbox Cleanup

Sent messages are retained for a configurable period (default: 7 days) for diagnostics, then deleted.

```sql
-- Periodic cleanup job
DELETE FROM outbox_messages
WHERE status = 'Sent' AND sent_at < datetime('now', '-7 days');

-- Expire messages past their TTL
UPDATE outbox_messages
SET status = 'Expired'
WHERE status IN ('Pending', 'Failed')
  AND expires_at < datetime('now');
```

### 6.2 Inbox Cleanup

Inbox records are retained long enough that any in-flight retry from the sender will still find them. Default retention: 24 hours after processing.

```sql
DELETE FROM inbox_messages
WHERE expires_at < datetime('now');
```

### 6.3 Cleanup Schedule

A background job runs cleanup at a configurable interval (default: every 5 minutes).

```csharp
public class RetentionCleanupJob : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await _outboxStore.CleanupSent(_options.OutboxRetention);
            await _outboxStore.ExpireStale();
            await _inboxStore.Cleanup();

            await Task.Delay(_options.CleanupInterval, ct);
        }
    }
}
```

### 6.4 Retention Tuning

| Setting | Default | Guidance |
|---------|---------|----------|
| Outbox sent retention | 7 days | Keep longer if you need audit trail |
| Outbox message TTL | 24 hours | Increase for workflows with long timeouts |
| Inbox retention | 24 hours | Must be longer than sender's max retry window |
| Cleanup interval | 5 minutes | Lower for high-throughput systems |

**Critical rule:** Inbox retention **must exceed** the sender's maximum retry window. If the sender can retry for up to 1 hour, the inbox must retain records for at least 2 hours to safely deduplicate late retries.

---

## 7. Transport Layer Integration

The outbox/inbox pattern is transport-agnostic. The Outbox Dispatcher uses a pluggable `ITransport` interface:

```csharp
public interface ITransport
{
    Task<TransportResponse> Send(OutboxMessage message);
}

public class TransportResponse
{
    public bool Acknowledged { get; set; }
    public bool DuplicateDetected { get; set; }
    public string? Payload { get; set; }
    public string? Error { get; set; }
}
```

### 7.1 Transport Implementations

| Transport | When to Use | Endpoint Format |
|-----------|-------------|-----------------|
| **HTTP/REST** | Cross-network, standard deployments | `https://host:port/_rf/receive` |
| **gRPC** | High-performance, cross-service | `grpc://host:port/RFReceiver/Receive` |
| **Named Pipes** | Same machine, Windows | `pipe://rf-{serviceId}` |
| **Unix Domain Sockets** | Same machine, Linux | `unix:///tmp/rf-{serviceId}.sock` |
| **In-Process** | Same process (testing, monolith) | Direct method call, no serialization |

### 7.2 Transport Selection

```csharp
services.AddResumableFunctions(options =>
{
    // Default transport for all destinations
    options.DefaultTransport = TransportType.Http;

    // Override for specific services
    options.MapService("inventory-service", TransportType.GrpcTransport, "grpc://inventory:5001");
    options.MapService("email-service", TransportType.NamedPipe, "pipe://rf-email");
    options.MapService("local-worker", TransportType.InProcess);
});
```

---

## 8. Failure Scenarios & Guarantees

| Scenario | What Happens | Outcome |
|----------|-------------|---------|
| Sender crashes before outbox write | Message was never persisted | Business operation also rolled back (same transaction). No message lost — operation never happened. |
| Sender crashes after outbox write, before send | Message is in outbox with status `Pending` | Dispatcher picks it up after restart. Message is sent. ✅ |
| Network failure during send | Sender gets no ACK | Dispatcher marks as failed, schedules retry. ✅ |
| Receiver crashes before processing | Sender gets no ACK | Sender retries. Receiver processes on next attempt. ✅ |
| Receiver crashes after processing, before inbox write | Transaction rolled back — neither business effect nor inbox record persisted | Sender retries. Receiver processes again (first effective time). ✅ |
| Receiver processes + inbox write succeeds, ACK lost | Inbox record exists | Sender retries. Receiver finds inbox record, returns cached response. No duplicate processing. ✅ |
| Receiver processes, sender retries due to timeout | Inbox record exists | Duplicate detected. Cached response returned. ✅ |
| Message expires before delivery | Outbox status → `Expired` | Application can handle via dead-letter callback or compensating action. ✅ |
| Sender and receiver both crash simultaneously | Outbox has the message, inbox does not | After both restart, dispatcher retries. Receiver processes. ✅ |
| SQLite file corruption | Outbox or inbox data lost | **Unrecoverable** — requires WAL mode + periodic backups as mitigation. ⚠️ |

---

## 9. SQLite Configuration

### 9.1 Recommended Pragmas

```sql
PRAGMA journal_mode = WAL;          -- Write-ahead logging for concurrent reads
PRAGMA synchronous = NORMAL;        -- Balance between durability and performance
PRAGMA busy_timeout = 5000;         -- Wait up to 5s for locks
PRAGMA cache_size = -8000;          -- 8MB cache
PRAGMA foreign_keys = ON;
PRAGMA temp_store = MEMORY;
```

### 9.2 WAL Mode Justification

WAL mode is essential because:

- The Outbox Dispatcher reads pending messages **while** the application writes new ones.
- The cleanup job deletes old records **while** new messages are being inserted.
- WAL allows concurrent readers and a single writer without blocking.

### 9.3 Database File Strategy

Each service maintains its own SQLite database file:

```
/data/{service-id}/
    outbox.db           -- Outbox messages
    outbox.db-wal       -- WAL file
    outbox.db-shm       -- Shared memory file
    inbox.db            -- Inbox messages
    inbox.db-wal
    inbox.db-shm
```

Separate files for outbox and inbox to minimize write contention.

### 9.4 Backup Strategy

Since SQLite is a file, backup is straightforward:

```csharp
// Online backup using SQLite backup API
await using var backup = _db.BackupInit("main", backupDb, "main");
backup.Step(-1); // Copy all pages
```

Recommended: backup every hour, retain for 7 days.

---

## 10. Source Generation

The developer should not write outbox/inbox plumbing code. The framework uses C# Source Generators to produce:

1. **Outbox wrappers** — for every `[Signal]` or `[Command]` definition, generate a method that writes to the outbox instead of sending directly.
2. **Shadow Controllers** — for every service endpoint that receives RF messages, generate an ASP.NET controller (or gRPC service) that handles inbox dedup + dispatch.
3. **Serialization code** — strongly-typed serialization/deserialization for message payloads.

### 10.1 Developer Experience

The developer writes:

```csharp
[Signal("OrderApproved")]
public record OrderApprovedSignal(Guid OrderId, string ApprovedBy);
```

The source generator produces:

- An outbox-backed `SendOrderApprovedSignal(...)` method.
- A shadow controller endpoint that receives this signal, deduplicates, and dispatches to the engine.
- Serialization code for `OrderApprovedSignal`.

### 10.2 Compile-Time Validation

The source generator also validates at compile time:

- All signal/command payload types are serializable.
- No circular references in payload types.
- Endpoint names are unique within a service.
- HMAC shared secrets are configured for all registered destinations.

---

## 11. Observability

### 11.1 Metrics

| Metric | Description |
|--------|-------------|
| `rf_outbox_pending_count` | Number of messages waiting to be sent |
| `rf_outbox_send_duration_ms` | Time to send a message (transport latency) |
| `rf_outbox_retry_count` | Number of retries per message (histogram) |
| `rf_outbox_failed_count` | Messages that exceeded max retries |
| `rf_outbox_expired_count` | Messages that expired before delivery |
| `rf_inbox_duplicate_count` | Duplicate messages detected |
| `rf_inbox_process_duration_ms` | Time to process an incoming message |
| `rf_inbox_size` | Current inbox table row count |

### 11.2 Diagnostic Endpoints

```
GET /_rf/health                    -- Overall health check
GET /_rf/outbox/stats              -- Outbox statistics (pending, failed, expired counts)
GET /_rf/outbox/failed             -- List of failed messages (for manual retry)
POST /_rf/outbox/{messageId}/retry -- Manually retry a failed message
GET /_rf/inbox/stats               -- Inbox statistics (size, duplicate rate)
```

### 11.3 Logging

Structured logging at key points:

```
[INF] Outbox: Message {MessageId} enqueued for {Destination}/{Endpoint}
[INF] Outbox: Message {MessageId} sent successfully ({Duration}ms)
[WRN] Outbox: Message {MessageId} send failed, retry {RetryCount}/{MaxRetries}: {Error}
[ERR] Outbox: Message {MessageId} permanently failed after {MaxRetries} retries
[INF] Inbox:  Message {MessageId} processed from {SourceServiceId}
[WRN] Inbox:  Duplicate {MessageId} detected from {SourceServiceId}
[WRN] Inbox:  HMAC verification failed for {MessageId} from {SourceServiceId}
```

---

## 12. Configuration Summary

```csharp
services.AddResumableFunctions(options =>
{
    // Outbox
    options.Outbox.DatabasePath = "data/outbox.db";
    options.Outbox.PollingInterval = TimeSpan.FromSeconds(1);
    options.Outbox.DefaultMaxRetries = 5;
    options.Outbox.DefaultMessageTTL = TimeSpan.FromHours(24);
    options.Outbox.BaseRetryDelay = TimeSpan.FromSeconds(2);
    options.Outbox.MaxRetryDelay = TimeSpan.FromMinutes(5);
    options.Outbox.BatchSize = 50;
    options.Outbox.SentRetention = TimeSpan.FromDays(7);

    // Inbox
    options.Inbox.DatabasePath = "data/inbox.db";
    options.Inbox.RetentionPeriod = TimeSpan.FromHours(24);
    options.Inbox.CleanupInterval = TimeSpan.FromMinutes(5);

    // Security
    options.Security.HmacAlgorithm = "HMAC-SHA256";
    options.Security.SharedSecrets = new Dictionary<string, string>
    {
        ["inventory-service"] = "base64-encoded-secret",
        ["email-service"] = "base64-encoded-secret"
    };

    // Transport
    options.DefaultTransport = TransportType.Http;
    options.MapService("inventory-service", TransportType.Http, "https://inventory:5000");
    options.MapService("email-service", TransportType.NamedPipe, "pipe://rf-email");
});
```

---

## 13. Limitations & Trade-offs

| Trade-off | Implication |
|-----------|-------------|
| **SQLite single-writer** | Only one process per database file can write at a time. High-throughput systems may need write batching or sharding by destination. |
| **Polling-based dispatch** | Introduces latency equal to the polling interval (default 1s). Acceptable for most workflow scenarios; not suitable for sub-millisecond requirements. |
| **No ordering guarantee** | Messages may arrive out of order. Workflow-level ordering is handled by the engine, not the transport. |
| **File-based storage** | SQLite is a file on disk. Requires proper backup strategy. Not suitable for containerized environments without persistent volumes. |
| **Inbox retention window** | If the sender retries after the inbox retention period, the receiver cannot detect the duplicate. Retention must be configured carefully relative to the retry window. |
| **Cached responses in inbox** | Storing response payloads increases inbox storage. Large response payloads should be kept small or stored by reference. |