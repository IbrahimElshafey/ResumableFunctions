namespace ResumableFunctions.Handler.InOuts;
public class ImpactedWorkflowsIds
{

    public int AffectedServiceId { get; internal set; }
    public string AffectedServiceName { get; internal set; }
    public string AffectedServiceUrl { get; internal set; }
    public List<int> AffectedFunctionsIds { get; internal set; }

    public long CallId { get; internal set; }
    public string MethodUrn { get; internal set; }
    public int MethodGroupId { get; internal set; }
    public DateTime CallDate { get; internal set; }


    public override string ToString()
    {
        return $"Put pushed call [{MethodUrn}:{CallId}] in the processing queue for service [{AffectedServiceName}].";
    }
}
