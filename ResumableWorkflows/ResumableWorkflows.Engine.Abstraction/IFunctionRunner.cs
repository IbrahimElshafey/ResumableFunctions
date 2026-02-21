using ResumableWorkflows.Engine.Abstraction.Contracts;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ResumableWorkflows.Engine.Abstraction
{
    public interface IFunctionRunner : IAsyncEnumerator<Wait>
    {
        Wait Current { get; }
        WaitStorageEntity WaitStorageEntity { get; }
        int State { get; }
        ValueTask DisposeAsync();
        ValueTask<bool> MoveNextAsync();
    }
}
