using ResumableFunctions.Handler.InOuts.Entities;

namespace ResumableFunctions.Handler.UiService.InOuts
{
    public record MethodGroupInfo(
        MethodsGroup Group, int MethodsCount, int ActiveWaits, int CompletedWaits, int CanceledWaits, DateTime Created)
    {
        public int AllWaitsCount => ActiveWaits + CompletedWaits + CanceledWaits;
    }
}
