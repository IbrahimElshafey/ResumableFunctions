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
