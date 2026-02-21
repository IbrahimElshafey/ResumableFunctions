using ResumableFunctions.Handler.InOuts;
using ResumableFunctions.Handler.InOuts.Entities;

namespace ResumableFunctions.Handler.UiService.InOuts
{
    public record FunctionInstanceInfo(ResumableFunctionState FunctionState, WaitEntity CurrentWait, int WaitsCount, int Id)
    {
        public string StateColor => FunctionState.Status switch
        {
            FunctionInstanceStatus.New => "black",
            FunctionInstanceStatus.InProgress => "yellow",
            FunctionInstanceStatus.Completed => "green",
            FunctionInstanceStatus.InError => "red",
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}
