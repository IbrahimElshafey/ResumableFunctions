using ResumableWorkflows.Engine.Abstraction.Contracts;
using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ResumableWorkflows.Engine.Abstraction
{
    public interface ISignalReceiver
    {
        Task<SignalReceiveResult> ReceiveSignal(Signal signal);
    }
}
