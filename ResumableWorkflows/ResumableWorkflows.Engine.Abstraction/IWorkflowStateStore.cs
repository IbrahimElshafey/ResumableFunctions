using ResumableWorkflows.Engine.Abstraction.Contracts;
using System;
using System.Threading.Tasks;

namespace ResumableWorkflows.Engine.Abstraction
{
    /// <summary>
    /// Represents the interface for persisting and retrieving workflow state.
    /// </summary>
    public interface IWorkflowStateStore
    {
        Task<WorkflowState> GetWorkflowStateAsync(Guid workflowId);
        Task<bool> SaveWorkflowStateAsync(WorkflowState state);
    }
}
