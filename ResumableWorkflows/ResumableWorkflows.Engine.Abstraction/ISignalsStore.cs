using ResumableWorkflows.Engine.Abstraction.Contracts;
using System;
using System.Threading.Tasks;

namespace ResumableWorkflows.Engine.Abstraction
{
    /// <summary>
    /// Represents the interface for storing and retrieving signals.
    /// </summary>
    public interface ISignalsStore
    {
       Task<Guid> SaveSignal(Signal signal);
        Task<Signal> GetSignal(Guid signalId);
         Task DeleteSignal(Guid signalId);
         Task DeleteSignals(Func<Signal, bool> predicate);
         Task<bool> SignalExists(Guid signalId);
    }
}
