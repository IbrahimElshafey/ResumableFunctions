using ResumableFunctions.Handler.InOuts.Entities.EntityBehaviour;

namespace ResumableFunctions.Handler.InOuts.Entities;

public class WaitProcessingRecord : IEntity<long>, IEntityWithUpdate
{
    public long Id { get; internal set; }
    public long PushedCallId { get; internal set; }
    public long WaitId { get; internal set; }
    public int? ServiceId { get; internal set; }
    public int FunctionId { get; internal set; }
    public int StateId { get; internal set; }
    public int TemplateId { get; internal set; }
    public MatchStatus MatchStatus { get; internal set; } = MatchStatus.ExpectedMatch;
    public ExecutionStatus AfterMatchActionStatus { get; internal set; } = ExecutionStatus.NotStartedYet;
    public ExecutionStatus ExecutionStatus { get; internal set; } = ExecutionStatus.NotStartedYet;
    public DateTime Created { get; internal set; }

    public DateTime Modified { get; internal set; }
    public string ConcurrencyToken { get; internal set; }

    public override bool Equals(object obj)
    {
        if (obj is WaitProcessingRecord waitProcessingRecord)
        {
            return
                waitProcessingRecord.WaitId == WaitId &&
                waitProcessingRecord.FunctionId == FunctionId &&
                waitProcessingRecord.StateId == StateId;
        }
        return base.Equals(obj);
    }

    public override int GetHashCode()
    {
        return $"{WaitId}-{FunctionId}-{StateId}".GetHashCode();
    }
}
