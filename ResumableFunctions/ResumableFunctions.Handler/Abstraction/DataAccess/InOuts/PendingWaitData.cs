using ResumableFunctions.Handler.InOuts.Entities;
using System.Linq.Expressions;

namespace ResumableFunctions.Handler.Abstraction.DataAccess.InOuts
{
    public record ExpectedWaitMatch(
        long WaitId,
        int TemplateId,
        int RequestedByFunctionId,
        int FunctionStateId,
        int? MethodToWaitId,
        long? ClosureDataId,
        long? LocalsId,
        LambdaExpression CallMandatoryPartExpression,
        string MandatoryPart,
        bool IsFirst);
    public record PendingWaitData(long WaitId, WaitTemplate Template, string MandatoryPart, bool IsFirst);
}
