namespace ResumableFunctions.Handler.InOuts;

public class TimeWaitInput
{
    public string TimeMatchId { get; set; }
    public string Description { get; set; }
    public int RequestedByFunctionId { get; set; }

    public override string ToString()
    {
        return Description;
    }
}
