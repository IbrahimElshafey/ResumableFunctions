Below is a list of **common or “standard” workflow patterns** (based on well-known references like the Workflow Patterns Initiative and BPMN) and an explanation of how **Resumable Functions (RF)** can handle each one. While the precise names and classifications can vary, these cover the core scenarios most developers encounter in workflow/orchestration.

* * *

1\. Basic Control-Flow Patterns
===============================

1.1 Sequence
------------

**Definition**: A series of tasks performed one after the other (e.g., Task A then Task B then Task C).

**How RF Handles It**:

*   Simply write your C# method in an **imperative** style:
    
    csharp
    
    Copy code
    
    `public async IAsyncEnumerable<Wait> SimpleSequence() {     // Step 1: wait for something or do something     yield return WaitMethod(...);     // Step 2: next wait     yield return WaitMethod(...);     // Step 3: final wait or action     yield return WaitMethod(...); }`
    
*   Each `yield return Wait(...)` ensures step N finishes (or an event is received) before proceeding to step N+1.

* * *

1.2 Parallel Split (a.k.a. Fork)
--------------------------------

**Definition**: A single thread of execution splits into multiple parallel threads (e.g., A -> B & C in parallel).

**How RF Handles It**:

*   Use **`WaitGroup(...)`** with multiple waits. Each wait can correspond to a method call or sub-workflow. This effectively waits in **parallel** for each event:
    
    csharp
    
    Copy code
    
    `yield return WaitGroup(new[] {     WaitMethod(...), // parallel 1     WaitMethod(...), // parallel 2 }, "Parallel Branch");`
    
*   The engine is “listening” for either or all of those calls, depending on how you define your match condition (see below).

* * *

1.3 Synchronization (a.k.a. Join)
---------------------------------

**Definition**: Multiple parallel threads converge back into a single flow (e.g., after B & C finish, continue with D).

**How RF Handles It**:

*   **`WaitGroup(...).MatchAll()`** or `.MatchIf(group => group.CompletedCount == group.WaitsCount)` can effectively act as a **join**—it waits until all the parallel sub-waits are satisfied, then moves on.
*   In code, you have something like:
    
    csharp
    
    Copy code
    
    `yield return   WaitGroup(new[]   {     WaitMethod(...),     WaitMethod(...),   }, "Parallel tasks")   .MatchIf(group => group.CompletedCount == 2); // After this line, both tasks have completed`
    

* * *

1.4 Exclusive Choice (a.k.a. XOR-Split)
---------------------------------------

**Definition**: A branching point where only one of several paths is chosen based on some condition (e.g., if condition X, do A; else do B).

**How RF Handles It**:

*   You can implement an **if-else** in normal C# code. For instance:
    
    csharp
    
    Copy code
    
    `if (someCondition) {     yield return WaitMethod(...); // Path A } else {     yield return WaitMethod(...); // Path B }`
    
*   Alternatively, you might do a single wait that branches internally after an event is matched. But typically, plain old if/else is the easiest approach in RF.

* * *

1.5 Simple Merge (a.k.a. XOR-Join)
----------------------------------

**Definition**: Multiple incoming branches that **do not** run in parallel converge into one path. Whichever branch finishes first continues the flow (or once any active branch finishes).

**How RF Handles It**:

*   You can handle it with standard C# branching, or if you treat them as parallel threads, a **WaitGroup** with `.MatchAny()` can effectively do an **or-join**.
*   For instance:
    
    csharp
    
    Copy code
    
    `yield return WaitGroup(   new[]   {      WaitMethod(...), // branch 1      WaitMethod(...), // branch 2   },   "Either-or scenario")   .MatchFirst(); // or .MatchIf(group => group.CompletedCount == 1) // Once the first wait is satisfied, we proceed`
    

* * *

2\. Advanced Branching & Synchronization Patterns
=================================================

2.1 Multi-Choice (a.k.a. OR-Split)
----------------------------------

**Definition**: Based on certain conditions, **more than one** (but not necessarily all) branches can be triggered.

**How RF Handles It**:

*   Because RF is code-centric, you can do something like:
    
    csharp
    
    Copy code
    
    `var triggers = new List<Wait>(); if (conditionA) triggers.Add(WaitMethod(...)); if (conditionB) triggers.Add(WaitMethod(...)); if (conditionC) triggers.Add(WaitMethod(...));  yield return WaitGroup(triggers, "Conditional parallel starts")     .MatchAll(); // or another aggregator logic`
    
*   This approach spawns whichever branches apply, all in parallel.

* * *

2.2 Multi-Merge (a.k.a. OR-Join without synchronization)
--------------------------------------------------------

**Definition**: Multiple possible branches converge, but you don’t need all of them to finish—once any active branch completes, you proceed; additional completions do nothing special.

**How RF Handles It**:

*   Similar to simple merge, you can do a **WaitGroup** plus `.MatchFirst()` or `.MatchIf(group => group.CompletedCount >= 1)` and ignore further completions.

* * *

2.3 Discriminator (a.k.a. First Join or “Wait for first completion, then proceed, but ignore others.”)
------------------------------------------------------------------------------------------------------

**Definition**: You start multiple parallel branches, but you only need the **first** one that completes. Once the first branch is done, you move on and don’t wait for others.

**How RF Handles It**:

*   Exactly as above, use a **WaitGroup** with a match condition that triggers on the first completion:
    
    csharp
    
    Copy code
    
    `yield return WaitGroup(   new[] { WaitMethod(...), WaitMethod(...) },   "Multiple parallel tasks" ).MatchIf(group => group.CompletedCount == 1); // proceed after first success`
    

* * *

2.4 N-out-of-M Join (Partial Join)
----------------------------------

**Definition**: You have **M** parallel tasks, but you want to proceed once **N** of them have completed. For example, 2 out of 4 managers must approve.

**How RF Handles It**:

*   **WaitGroup** with a dynamic condition:
    
    csharp
    
    Copy code
    
    `yield return   WaitGroup(new []   {     WaitMethod(...),     WaitMethod(...),     WaitMethod(...),     WaitMethod(...),   },   "4 manager approvals")   .MatchIf(group => group.CompletedCount >= 2);`
    
*   Then you can store how many actually approved, or even stop listening to the rest if you prefer.

* * *

3\. Structural Patterns
=======================

3.1 Structured Loop (Iterating)
-------------------------------

**Definition**: A loop repeating a workflow step until a condition is met (e.g., “Keep trying this step until success or we’ve tried 3 times”).

**How RF Handles It**:

*   Use **standard C#** loops (`while`, `for`, `foreach`) with `yield return Wait(...)` inside.
*   For instance, a retry loop:
    
    csharp
    
    Copy code
    
    `int attempts = 0; while (attempts < 3) {     attempts++;     yield return WaitMethod(...).MatchIf((input, output) => output == true);     if (conditionMet) break; }`
    
*   The function can pause at each iteration. If it times out or fails, it can either re-run or break out.

* * *

3.2 Sub-Workflow (a.k.a. Call Activity or Sub-Process)
------------------------------------------------------

**Definition**: The workflow can call another workflow (sub-workflow) and wait for it to complete.

**How RF Handles It**:

*   **`WaitFunction(...)`** allows you to call a **nested** or **child** resumable function. You can then `.MatchIf(...)` on its output.
*   This sub-function can itself contain multiple waits, groups, etc.
    
    csharp
    
    Copy code
    
    `yield return WaitFunction(ManagerThreeSubFlow(), "SubFlow")   .MatchIf(output => output == true);`
    

* * *

4\. Event & Time-Based Patterns
===============================

4.1 Deferred Choice (a.k.a. Race with a Timer)
----------------------------------------------

**Definition**: Wait for either an event to occur **or** a timer to expire (whichever happens first).

**How RF Handles It**:

*   Combine `WaitMethod(...)` and `WaitDelay(...)` in a **WaitGroup**:
    
    csharp
    
    Copy code
    
    `yield return WaitGroup(   new []   {     WaitMethod(...),     WaitDelay(TimeSpan.FromMinutes(5))   },   "Method or Timeout" ).MatchIf(group => group.CompletedCount == 1);`
    
*   Once either the method is called or 5 minutes pass, you proceed.

* * *

4.2 Timer / Wait Delay / Wait Until
-----------------------------------

**Definition**: Pause the workflow until a certain time or after a given duration.

**How RF Handles It**:

*   **`WaitDelay(TimeSpan)`** or **`WaitUntil(DateTime)`**. The engine will store your function state and effectively schedule a resume after the specified delay or time. No active thread is waiting in memory.

* * *

5\. Data Handling & Condition Patterns
======================================

5.1 Conditional Flow / Data-Driven Choice
-----------------------------------------

**Definition**: The path taken depends on some data value (e.g., output of a previous step or an external parameter).

**How RF Handles It**:

*   Standard **C# if-else** or `.MatchIf(...)` with a more complex expression. You can also manipulate local variables in `AfterMatch(...)`.
*   Because you’re writing normal C# code, you can handle any data logic or condition checks inline.

* * *

5.2 State-Based Trigger (WaitWhile / WaitUntil Condition)
---------------------------------------------------------

**Definition**: Continue only when a certain condition in the system is true (e.g., a DB record changes status, or a variable is updated to a certain value).

**How RF Handles It**:

*   If you prefer an event-driven approach, treat the “status change” as a method call triggered from the DB or an external service.
*   Or you can do a polling-based or triggered approach. **RF** does not usually do continuous polling out-of-the-box (that’s more of a background job scenario), but you can orchestrate something like:
    
    csharp
    
    Copy code
    
    `while (someConditionNotMet) {     yield return WaitDelay(TimeSpan.FromMinutes(1));     // check condition again }`
    
*   Alternatively, push an event whenever the condition is met, using `[PushCall("StatusChange")]`.

* * *

6\. Other Patterns (Error Handling, Compensation)
=================================================

6.1 Exception Handling / Compensation (Saga-like)
-------------------------------------------------

**Definition**: If a failure occurs mid-workflow, you may need to roll back previous steps or apply “compensating transactions.”

**How RF Handles It**:

*   You can use normal **C# try/catch** around your waits or call compensating methods in the catch block.
*   For a more advanced saga pattern, you might define a sub-workflow that handles compensation steps.
*   There’s no built-in “compensation pattern” at the library level, but it’s straightforward to do in code since you have full control.

* * *

7\. Summary
===========

**Resumable Functions** can handle the vast majority of **common workflow patterns** by:

1.  **Yielding Waits** (`WaitMethod`, `WaitGroup`, `WaitDelay`, etc.).
2.  **Combining** them with normal C# constructs (if-else, loops, try-catch).
3.  **Using** group-level `.MatchIf(...)` to implement parallel splits, joins, partial aggregates, race conditions, etc.
4.  **Persisting** state so you can wait indefinitely for an event or a time-based trigger.

Because RF relies on **pure C#** for branching, looping, conditionals, and subroutines, you can replicate just about **any** standard workflow pattern without needing a specialized DSL. The library’s wait primitives (plus the concept of `[PushCall]` for external triggers) cover the event-driven aspect, while normal C# handles the logic flow.

Therefore, whether it’s **parallel flows, partial aggregates, sub-workflows, conditional branches, timers, or indefinite event waits**, RF provides the building blocks to implement virtually all known **workflow patterns**.