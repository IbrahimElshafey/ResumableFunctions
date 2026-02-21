using ResumableFunctions.Handler.InOuts.Entities;

namespace ResumableFunctions.MvcUi.DisplayObject
{
    public class LogsViewModel
    {
        public List<LogRecord> Logs { get; internal set; }
        public Dictionary<int, string> StatusCodes => Handler.Helpers.StatusCodes.StatusCodeNames;
        public List<ServiceData> Services { get; internal set; }
        public int SelectedService { get; internal set; }
        public int SelectedStatusCode { get; internal set; }
    }
}
