using ResumableFunctions.Handler.InOuts.Entities;

namespace ResumableFunctions.Handler.BaseUse
{
    public class Wait
    {
        internal Wait(WaitEntity wait)
        {
            WaitEntity = wait;
        }

        internal WaitEntity WaitEntity { get; set; }
    }
}