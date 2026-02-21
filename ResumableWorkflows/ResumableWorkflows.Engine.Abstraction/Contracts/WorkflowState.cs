using System;
using System.Collections.Generic;

namespace ResumableWorkflows.Engine.Abstraction.Contracts
{
    public class WorkflowState
    {
        public Guid Id { get; set; }
        public object State { get; set; }
    }
}
