using System;

namespace ResumableWorkflows.Engine.Abstraction.Contracts
{
    public class Signal
    {
        public Guid Id { get; }
        public object Input { get; }
        public object Output { get; }
        public string SignalUrn { get; }
    }
}
