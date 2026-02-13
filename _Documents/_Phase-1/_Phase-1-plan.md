This **Master Project Plan** consolidates the requirements from all uploaded documents into a structured roadmap. It eliminates redundancies (such as duplicate entries for NuGet packaging and pushed call deletion) while maintaining the technical specificity of your notes.

---

# 🚀 Resumable Functions: Master Project Plan

## 1. Core Engine & Refactoring

**Goal:** Improve reliability, performance, and clean up technical debt.

* **Attribute & Scan Logic:**
* Use `AsyncIteratorStateMachineAttribute` to accurately retrieve classes.
* Implement source generators for "no-scan" fast startup.
* Validate Method URN uniqueness and input/output serialization during scanning.


* **Data Access & Transactions:**
* Remove `SaveChangesAsync` from repositories; transition to **One Transaction per Business Unit**.
* Implement a Data Access Abstraction to support switching databases (SQL, In-Memory, etc.).
* Optimize Wait table indexes for high-concurrency insertion.


* **Code Cleanup:**
* Automate deletion of dead `MethodIdentifiers` (e.g., resumable functions with no active waits).
* Standardize on a composite primary key for logs: `(id, entity_id, entity_type)`.



## 2. Advanced Features & Concurrency

**Goal:** Support complex distributed scenarios and versioning.

* **Concurrency & Locking:**
* Review `BackgroundJobExecutor.ExecuteWithLock` positions.
* Handle unique index exceptions when multiple services add the same `MethodGroup` simultaneously.


* **Versioning (Side-by-Side):**
* Support multiple active versions; route pushed calls to the version that created the original wait.
* Automate "Dead Version" marking when no active waits remain to clean up resources.


* **Messaging Frameworks:**
* Abstract `IExternalCallHandler` to support HTTP, gRPC, and IPC.



## 3. Data Store Strategy

**Goal:** Tiered storage for performance and scalability.

* **Hot/Cold Storage:** Implement a system that keeps active ("hot") data in memory while offloading "cold" data to disk.
* **Pushed Calls:** Optimize for fast insertion and queries by `Service ID`, `Date`, and `Method URN`.
* **Logging:** Move logs to a dedicated store (e.g., InfluxDB or RocksDB) and implement structured logging.
* **State Management:** Secure function states and match expressions with optional encryption.

## 4. UI & Observability (V1 & V2)

**Goal:** Provide administrative control and system health insights.

* **V1 Enhancements:**
* Decouple UI from EF context; restrict access to server-side only.
* Implement infinite scroll and date-range filters for Logs, Pushed Calls, and Instances.


* **V2 Management Tools:**
* **Manual Intervention:** Cancel waits, re-wait specific steps, or simulate a pushed call.
* **Health Checks:** "Everything Okay" dashboard to verify start waits, signature consistency, and serialization.



## 5. Testing & Validation

**Goal:** Ensure system stability under load.

* **Test Shell Improvements:**
* Enable parallel, in-memory unit testing.
* Auto-generate test code for resumable functions.


* **Performance Benchmarking:**
* Test 1 million active waits for a single function.
* Test high-throughput scenarios (10,000 pushed calls per second).


* **Roslyn Analyzers:**
* Build analyzers to catch missing `MatchExpression` or `AfterMatchAction` at compile time.
* Enforce that `ResumableFunctionsContainer` remains constructor-less.



## 6. DevOps & Distribution

**Goal:** Standardize deployment and integration.

* **Packaging:** Build a NuGet package targeting multiple runtimes and versions.
* **CI/CD:** Implement migration scripts for schema changes between versions and research Hangfire database schema migrations.
