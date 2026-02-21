using ResumableFunctions.Handler.InOuts;
using ResumableFunctions.Handler.UiService.InOuts;

namespace ResumableFunctions.MvcUi.DisplayObject
{
    public class FunctionInstancesModel
    {
        public string FunctionName { get; set; }
        public List<FunctionInstanceInfo> Instances { get; set; }

        public int InProgressCount => Instances.Count(x => x.FunctionState.Status == FunctionInstanceStatus.InProgress);
        public int FailedCount => Instances.Count(x => x.FunctionState.Status == FunctionInstanceStatus.InError);
        public int CompletedCount => Instances.Count(x => x.FunctionState.Status == FunctionInstanceStatus.Completed);
    }
}