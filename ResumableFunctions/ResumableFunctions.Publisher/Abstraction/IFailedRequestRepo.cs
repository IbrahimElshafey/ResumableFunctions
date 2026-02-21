using ResumableFunctions.Publisher.InOuts;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ResumableFunctions.Publisher.Abstraction
{
    public interface IFailedRequestRepo
    {
        Task Add(FailedRequest request);
        Task Update(FailedRequest request);
        Task Remove(FailedRequest request);
        Task<bool> HasRequests();
        IEnumerable<FailedRequest> GetRequests();
    }
}
