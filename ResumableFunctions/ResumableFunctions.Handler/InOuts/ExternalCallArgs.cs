namespace ResumableFunctions.Handler.InOuts;

public class ExternalCallArgs
{
    public string ServiceName { get; set; }
    public MethodData MethodData { get; set; }
    public object Input { get; set; }
    public object Output { get; set; }
    public DateTime Created { get; set; }
}