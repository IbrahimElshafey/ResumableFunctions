using ResumableWorkflows.Engine.Abstraction.Contracts;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ResumableWorkflows.Engine.Abstraction
{
    /// <summary>
    /// Represents the interface for storing and retrieving wait conditions.
    /// </summary>
    public interface IWaitsStore
    {
        Task<Guid> AddWait(Wait wait);
    }
}
