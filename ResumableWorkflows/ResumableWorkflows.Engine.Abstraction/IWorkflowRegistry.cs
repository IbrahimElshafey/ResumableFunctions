using ResumableWorkflows.Engine.Abstraction.Contracts;
using System;
using System.Threading.Tasks;

namespace ResumableWorkflows.Engine.Abstraction
{
    /// <summary>
    /// Represents the interface for registering and retrieving workflow definitions.
    /// </summary>
    public interface IWorkflowRegistry
    {
        Task<Wait> GetFirstWait(Guid workflowId);
    }
}