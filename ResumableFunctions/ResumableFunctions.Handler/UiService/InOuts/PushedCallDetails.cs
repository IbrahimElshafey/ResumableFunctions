using ResumableFunctions.Handler.InOuts;

namespace ResumableFunctions.Handler.UiService.InOuts
{
    public class PushedCallDetails
    {
        public string InputOutput { get; }
        public MethodData MethodData { get; }
        public List<MethodWaitDetails> Waits { get; }

        public PushedCallDetails(string inputOutput, MethodData methodData, List<MethodWaitDetails> waits)
        {
            InputOutput = inputOutput;
            MethodData = methodData;
            Waits = waits;
        }
    }
}
