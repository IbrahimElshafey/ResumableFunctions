using ResumableFunctions.Handler.InOuts.Entities;
using ResumableFunctions.Handler.UiService.InOuts;

namespace ResumableFunctions.MvcUi.DisplayObject
{
    public class PushedCallsViewMode
    {
        public List<PushedCallInfo> Calls { get; internal set; }
        public List<ServiceData> Services { get; internal set; }
        public int SelectedService { get; internal set; }
        public string SearchTerm { get; internal set; }
    }
}
