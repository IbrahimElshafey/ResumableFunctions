namespace ResumableFunctions.Handler.InOuts;

public class InputOutput
{
    public object Input { get; set; }
    public object Output { get; set; }

    internal static InputOutput FromGeneric(dynamic value)
    {
        return new InputOutput
        {
            Input = value.Input,
            Output = value.Output
        };
    }
}