using ResumableFunctions.Handler.InOuts.Entities;

namespace ResumableFunctions.Handler.UiService.InOuts
{
    public record PushedCallInfo
        (PushedCall PushedCall, int ExpectedMatchCount, int MatchedCount, int NotMatchedCount);
}
