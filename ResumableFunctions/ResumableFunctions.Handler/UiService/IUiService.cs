using ResumableFunctions.Handler.InOuts.Entities;
using ResumableFunctions.Handler.UiService.InOuts;

namespace ResumableFunctions.Handler.UiService
{
    public interface IUiService
    {
        Task<List<ServiceData>> GetServices();
        Task<List<LogRecord>> GetLogs(int page = 0, int serviceId = -1, int statusCode = -1);
        Task<List<ServiceInfo>> GetServicesSummary();
        Task<List<FunctionInfo>> GetFunctionsSummary(int serviceId = -1, string functionName = null);
        Task<List<MethodGroupInfo>> GetMethodGroupsSummary(int serviceId = -1, string searchTerm = null);
        Task<List<PushedCallInfo>> GetPushedCalls(int page = 0, int serviceId = -1, string searchTerm = null);
        Task<List<FunctionInstanceInfo>> GetFunctionInstances(int functionId);
        Task<PushedCallDetails> GetPushedCallDetails(long pushedCallId);
        Task<FunctionInstanceDetails> GetFunctionInstanceDetails(int instanceId);
        Task<List<MethodInGroupInfo>> GetMethodsInGroup(int groupId);
        Task<List<MethodWaitDetails>> GetWaitsInGroup(int groupId);
    }
}
