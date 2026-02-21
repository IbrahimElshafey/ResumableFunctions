using ResumableFunctions.Publisher.InOuts;
using System.Threading.Tasks;

namespace ResumableFunctions.Publisher.Abstraction
{
    public interface IFailedRequestHandler
    {
        Task EnqueueFailedRequest(FailedRequest failedRequest);
        Task HandleFailedRequests();
    }
}
