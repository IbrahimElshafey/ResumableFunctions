using ResumableFunctions.Handler.InOuts;
using ResumableFunctions.Handler.InOuts.Entities;
using System.Collections;

namespace ResumableFunctions.Handler.UiService.InOuts
{
    public class FunctionInstanceDetails
    {
        public string FunctionUrn { get; }
        public string FunctionName { get; }
        public int InstanceId { get; }
        public FunctionInstanceStatus Status { get; }
        public string InstanceData { get; }
        public DateTime Created { get; }
        public DateTime Modified { get; }
        public int ErrorsCount { get; }
        public ArrayList Waits { get; }
        public List<MethodWaitDetails> MethodWaitDetails { get; }
        public List<LogRecord> Logs { get; }
        public int FunctionId { get; }

        public FunctionInstanceDetails(
            int instanceId, int functionId, string name, string functionName, FunctionInstanceStatus status, string instanceData, DateTime created, DateTime modified, int errorsCount, ArrayList waits, List<LogRecord> logs)
        {
            InstanceId = instanceId;
            FunctionId = functionId;
            FunctionUrn = name;
            FunctionName = functionName;
            Status = status;
            InstanceData = instanceData;
            Created = created;
            Modified = modified;
            ErrorsCount = errorsCount;
            Waits = waits;
            Logs = logs;
        }
    }
}
