# Implementation Plan: Visual Workflow Designer (Phase 4)

This document details the architecture and functional requirements for the **Visual Workflow Designer**. The goal is to provide a "Drag-and-Drop" interface that allows users to orchestrate versioned services, define logic, and map data without writing raw C# code.

---

## 1. High-Level UI Architecture

The workspace is designed as a **Quad-Pane Interface**, optimized for a left-to-right discovery and configuration flow.

| Panel | Name | Purpose |
| --- | --- | --- |
| **Left** | **The Toolbox** | Tabbed panel containing Events (Triggers), Commands (Actions), and Logic Blocks. |
| **Center** | **The Canvas** | The visual workspace for drawing nodes and connectors (Arrows). |
| **Right (Inner)** | **Properties** | Configuration panel for the *selected* node (Matchers, Parameter Mapping). |
| **Right (Outer)** | **Scope/Context** | Real-time list of all data properties available from previous steps. |

---

## 2. Functional Requirements

### A. The Toolbox (Discovery)

The toolbox is populated dynamically via the **Engine Registry API**.

* **Events Tab:** Lists all methods decorated with `[PushCall]`. These act as the "Start" or "Waiting" nodes.
* **Commands Tab:** Lists all registered Service Interfaces and their methods.
* **Logic Tab:** Native engine blocks: `Wait All`, `Wait Any`, `Sub-Workflow`, and `Loop`.

### B. The Canvas & Connector Logic

The canvas supports two specific types of relationships:

* **Go To (Progressive):** A standard forward transition.
* **Back To (Recursive):** Points to a previous node in the flow. This visually represents a `while` loop or a state-machine "retry" logic.

### C. The Variable Scope Panel (Data Lineage)

As the user builds the flow, the UI maintains a "Context Tree." If Node A is an `OrderCreated` event, the Scope Panel expands to show all properties of the `Order` object.

* The scope is **additive**: each step adds its output to the available context for subsequent steps.

### D. The Properties Panel (Mapping & Logic)

This panel changes its schema based on the selected node:

* **For Events:** An expression builder to define a "Match." (e.g., `order.Total > 100`).
* **For Commands:** A dynamic form generated from the method's signature. Users can "pipe" variables from the Scope panel directly into these inputs.

---

## 3. Technical Implementation Strategy

### Frontend Technology Stack

* **Canvas Engine:** **React Flow** or **Svelte Flow**. These libraries provide the "Nodes and Edges" management and support cyclic (Back To) loops natively.
* **Expression Editor:** **Monaco Editor** (the VS Code core) to provide intellisense and syntax highlighting for C# expressions within the Properties panel.
* **State Management:** **Zustand** or **Redux** to track the "Global Scope" as the graph grows.

### Backend Bridge: The Metadata API

To keep the UI in sync with the C# code, the Engine must expose a Metadata endpoint:

1. **Reflection:** The Engine reflects on the Service Interfaces.
2. **JSON Schema:** It produces a schema describing the input/output types.
3. **UI Generation:** The Frontend reads this schema to render the Properties Panel and the Scope Tree.

---

## 4. Logical Mapping: UI to C#

The final output of the UI is a **Workflow Manifest (JSON)**. This manifest is sent to the Engine, which uses a **Source Generator** or **Roslyn Scripting** to compile it into a `ResumableFunction` class.

**Mapping Logic:**

* **Node ID**  Step Name.
* **Arrow (GoTo)**  Standard `yield return`.
* **Arrow (BackTo)**  A `while` loop or a `WaitUntil` condition.
* **Property Mapping**  Assignment logic in the generated C# method.

---

## 5. Security & Validation

* **Type Safety:** The UI must validate that a `string` property from the Scope isn't being mapped to an `int` parameter in a Command.
* **Loop Protection:** "Back To" arrows must be accompanied by a condition or a "Wait" block to prevent infinite execution loops that could crash the Engine.

---

## Summary of Implementation Phases

1. **Phase 4.1:** Build the 4-pane layout and basic React Flow canvas.
2. **Phase 4.2:** Implement the Metadata API in the Engine to feed the Toolbox.
3. **Phase 4.3:** Create the Properties Panel with dynamic mapping logic.
4. **Phase 4.4:** Develop the "Compiler" that transforms the Canvas JSON into a C# Resumable Function.

**Would you like me to draft the JSON schema for the "Workflow Manifest" that connects the UI to the Backend Compiler?**
