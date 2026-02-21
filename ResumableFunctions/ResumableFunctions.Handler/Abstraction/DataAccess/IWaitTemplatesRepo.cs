using ResumableFunctions.Handler.InOuts;
using ResumableFunctions.Handler.InOuts.Entities;

namespace ResumableFunctions.Handler.Abstraction.Abstraction
{
    public interface IWaitTemplatesRepo
    {
        Task<WaitTemplate> AddNewTemplate(
            byte[] hashResult,
            object currentFunctionInstance,
            int funcId,
            int groupId,
            int? methodId,
            int inCodeLine,
            string cancelMethodAction,
            string afterMatchAction,
            MatchExpressionParts matchExpressionParts);
        Task<WaitTemplate> AddNewTemplate(byte[] hashResult, MethodWaitEntity methodWait);
        Task<WaitTemplate> CheckTemplateExist(byte[] hash, int funcId, int groupId);
        Task<List<WaitTemplate>> GetWaitTemplatesForFunction(int methodGroupId, int functionId);
        Task<WaitTemplate> GetById(int templateId);
        Task<WaitTemplate> GetWaitTemplateWithBasicMatch(int methodWaitTemplateId);
    }
}