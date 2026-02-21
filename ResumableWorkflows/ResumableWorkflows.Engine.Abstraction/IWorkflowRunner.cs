using ResumableWorkflows.Engine.Abstraction.Contracts;
using System;
using System.Threading.Tasks;

namespace ResumableWorkflows.Engine.Abstraction
{
    /// <summary>
    /// Represents the interface for executing workflow instances.
    /// </summary>
    public interface IWorkflowRunner
    {
        Task<WorkflowExecutionResult> RunWorkflow(Guid workflowInstanceId);
    }
}
