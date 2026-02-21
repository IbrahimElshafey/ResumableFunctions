using ResumableWorkflows.Engine.Abstraction.Contracts;
using System;
using System.Threading.Tasks;

namespace ResumableWorkflows.Engine.Abstraction
{
    /// <summary>
    /// Represents the interface for loading workflow definitions from DLLs.
    /// </summary>
    public interface IWorkflowLoader
    {
        /// <summary>
        /// Loads a workflow definition from a DLL asynchronously.
        /// </summary>
        /// <param name="dllPath">The path to the DLL containing the workflow definition.</param>
        /// <returns>A task representing the asynchronous operation, containing the loaded workflow definition.</returns>
        Task<WorkflowDefinition> LoadWorkflowFromDllAsync(string dllPath,string workflowUrn);
    }
}
