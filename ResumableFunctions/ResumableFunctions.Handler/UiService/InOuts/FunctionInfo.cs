using ResumableFunctions.Handler.InOuts.Entities;

namespace ResumableFunctions.Handler.UiService.InOuts
{
    public record FunctionInfo(ResumableFunctionIdentifier FunctionIdentifier, string FirstWait, int InProgress, int Completed, int Failed);
}
