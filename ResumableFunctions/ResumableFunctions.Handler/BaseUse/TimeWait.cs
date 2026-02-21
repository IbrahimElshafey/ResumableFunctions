using ResumableFunctions.Handler.InOuts;
using ResumableFunctions.Handler.InOuts.Entities;

namespace ResumableFunctions.Handler.BaseUse
{
    public class TimeWait : Wait
    {
        internal TimeWaitEntity TimeWaitEntity { get; }

        internal TimeWait(TimeWaitEntity wait) : base(wait)
        {
            TimeWaitEntity = wait;
        }

        public Wait AfterMatch(Action<TimeWaitInput, bool> AfterMatchAction)
        {

            if (AfterMatchAction != null)
            {
                TimeWaitEntity._timeMethodWait.AfterMatch(AfterMatchAction);
            }
            return this;
        }
    }
}