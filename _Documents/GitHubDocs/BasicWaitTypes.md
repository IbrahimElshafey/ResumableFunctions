Below is an **updated** documentation for **Resumable Functions**, focusing on the key wait types, how to replay parts of your workflow with standard C# control flow, and additional details about **Wait for a Single Method**.

* * *

1\. Wait for a Single Method
----------------------------

A **single-method wait** is the simplest form of a wait in Resumable Functions. It is similar to an `await` in `async/await` but offers more control over when it completes, based on **input**, **output**, and additional metadata.

### Anatomy of a Single-Method Wait

csharp

Copy code

`yield return
    Wait<Project, bool>(ProjectSubmitted, "Project Submitted")
        .MatchIf((input, output) => output == true)
        .AfterMatch((input, output) => CurrentProject = input);` 

1.  **Signature**: `Wait<TInput, TOutput>(methodIdentifier, name)`
    
    * **`TInput`** represents the input type passed into the method call.
    * **`TOutput`** represents the type returned from the method call.
2.  **Method Identifier** (`ProjectSubmitted` above)  
    This is a reference to the actual method that the Resumable Function is listening for.
    
3.  **Name** (`"Project Submitted"`)  
    A human-readable label for the wait, useful for debugging or referencing in logs.
    
4.  **MatchIf**: `(input, output) => output == true`  
    Defines the condition under which the wait is fulfilled. In the example, the wait completes only if the method’s output is `true`.
    
5.  **AfterMatch**: `(input, output) => CurrentProject = input`  
    A callback that executes right after the match condition is satisfied. You can use this to update local or global state—like storing `input` in a variable.
    

### Input/Output in Waits

* **Input**: The data passed to your **pushed call**. When an external caller (or another part of your system) invokes the `ProjectSubmitted` method, it might supply some input—an instance of `Project`, for instance.
* **Output**: The return value from the `ProjectSubmitted` method. If the method returns a `bool`, that becomes your `output`.
* **Push Call Attribute**: In Resumable Functions, you typically decorate methods with a _Push Call Attribute_ (e.g., `[PushCall("ProjectSubmitted")]`) to indicate they can be invoked externally and have the potential to satisfy a wait. This attribute helps the Resumable Functions engine route the incoming calls to the correct waiting points.

### Why Use `AfterMatch`?

When your wait condition is satisfied (e.g., `output == true`), you often need to react to the result—updating a database, saving a field, modifying global state, etc. The `AfterMatch` clause is precisely for that. It cleanly separates the **logic of matching** (i.e., whether to continue the flow) from the **logic of handling** what happens once the wait is resolved.

* * *

2\. Wait for the First Method Match in a Group
----------------------------------------------

This wait type is analogous to `Task.WhenAny()`. Execution resumes as soon as **any single method** in the group produces a matching result.

csharp

Copy code

`yield return Wait("Wait First In Three",
    Wait<string, string>(Method7, "Method 7"),
    Wait<string, string>(Method8, "Method 8"),
    Wait<string, string>(Method9, "Method 9")
).MatchAny(); // or .MatchFirst()` 

* **`MatchAny` / `MatchFirst`**: Signals that the **first** successfully matched method call will satisfy the wait.

* * *

3\. Wait for a Group of Methods
-------------------------------

This wait type is analogous to `Task.WhenAll()`. Execution proceeds only after **all** methods in the group have produced a matching result.

csharp

Copy code

`yield return Wait("Wait three methods",
    Wait<string, string>(Method1, "Method 1"),
    Wait<string, string>(Method2, "Method 2"),
    Wait<string, string>(Method3, "Method 3")
);

// Or explicitly call .MatchAll()
yield return Wait("Wait three methods",
    Wait<string, string>(Method1, "Method 1"),
    Wait<string, string>(Method2, "Method 2"),
    Wait<string, string>(Method3, "Method 3")
).MatchAll();` 

* * *

4\. Custom Wait for a Group
---------------------------

You can define a **custom completion condition** using a lambda expression. The group is marked as complete only when the condition is met.

csharp

Copy code

`yield return Wait("Wait three methods",
    Wait<string, string>(Method1, "Method 1"),
    Wait<string, string>(Method2, "Method 2"),
    Wait<string, string>(Method3, "Method 3")
)
.MatchIf(waitsGroup => waitsGroup.CompletedCount == 2 && Id == 10 && x == 1);` 

* * *

5\. Wait for a Sub-Resumable Function
-------------------------------------

You can **wait** for an internal sub-resumable function (another set of waits defined in the same codebase, but not an entry point).

csharp

Copy code

`yield return Wait("Wait sub function that waits two manager approvals.", WaitTwoManagers);

// Mark the sub function with [SubResumableFunction(...)]
[SubResumableFunction("WaitTwoManagers")]
public async IAsyncEnumerable<Wait> WaitTwoManagers()
{
    // Implementation of the sub-resumable function
    // yield return Wait(...) etc.
}` 

* * *

6\. Nesting Sub-Resumable Functions
-----------------------------------

Sub-resumable functions can, in turn, wait for other sub-resumable functions, allowing you to compose layered workflows.

csharp

Copy code

`[SubResumableFunction("SubFunction1")]
public async IAsyncEnumerable<Wait> SubFunction1()
{
    yield return Wait<string, string>(Method1, "M1").MatchAny();
    yield return Wait("Wait sub function2", SubFunction2);
}` 

* * *

7\. Mixed Group Wait
--------------------

You can create a mixed group that includes:

* **Method waits** (direct method calls)
* **Sub-resumable functions**
* **Nested groups of waits**

csharp

Copy code

`yield return Wait("Wait Many Types Group",
    Wait("Wait three methods in Group",
        Wait<string, string>(Method1, "Method 1"),
        Wait<string, string>(Method2, "Method 2"),
        Wait<string, string>(Method3, "Method 3")
    ),
    Wait("Wait sub function", SubFunction),
    Wait<string, string>(Method5, "Wait Single Method")
);` 

* * *

8\. Replaying or “Going Back” Using Standard C# Control Flow
------------------------------------------------------------

Rather than dedicated `GoBack` methods, you can simply rely on **standard C#** constructs like `goto`, loops, or recursion. Below is an example using **labels** and `goto` to replay a previous wait:

csharp

Copy code

`[ResumableFunctionEntryPoint("GoBeforeFunction")]
public async IAsyncEnumerable<Wait> Test()
{
    int localCounter = 10;

    // First wait
    yield return WaitMethod<string, string>(Method1, "M1");

before_m2:
    localCounter += 10;
    Counter += 10;

    // Second wait
    yield return WaitMethod<string, string>(Method2, "M2")
        .MatchAny() // or .MatchFirst()
        .AfterMatch((_, _) => localCounter += 10);

    // Replay logic: if condition is not met, go back and wait for the same method again
    if (Counter < 20)
        goto before_m2;

    // Some validation
    if (localCounter != 50)
        throw new Exception("Local variable should be 50");
    if (Counter != 20)
        throw new Exception("Counter should be 20");

    await Task.Delay(100);
}` 

* You can similarly use `while` loops, `for` loops, or even recursive calls to achieve “go-back” behavior.

* * *

9\. Time-Based Waits
--------------------

There are two ways to pause execution based on time:

1.  **`WaitDelay(TimeSpan, string?)`** – Suspends execution for the specified duration.
2.  **`WaitUntil(DateTime, string?)`** – Suspends execution until the specified date and time.

### `WaitDelay` Example

csharp

Copy code

`yield return WaitDelay(TimeSpan.FromDays(2), "Wait Two Days")
    .AfterMatch(x => TimeWaitId = x.TimeMatchId);` 

### `WaitUntil` Example

csharp

Copy code

`yield return WaitUntil(new DateTime(2025, 01, 01), "Wait Until 1st Jan 2025")
    .AfterMatch(x => TimeWaitId = x.TimeMatchId);` 

* * *

Summary
-------

Resumable Functions provides a powerful, event-driven model for building asynchronous workflows:

* **Wait for Single or Multiple Methods** with fine-grained matching logic (`MatchIf`, `MatchAny`, `MatchAll`, etc.).
* **React after a Match** with `AfterMatch` to update state and trigger side effects.
* **Compose** sub-resumable functions for multi-layered workflows.
* **Replay** or “Go Back” to previous steps purely through **native C#** control-flow constructs.
* **Wait by Time** (either by delaying a duration or waiting until a specific date).

With these features, you can create readable, maintainable, and complex workflows that make asynchronous logic simpler to manage.