using ResumableFunctions.Handler.InOuts.Entities.EntityBehaviour;

namespace ResumableFunctions.Handler.InOuts.Entities;
public class LockState : IEntity<int>
{
    public int Id { get; internal set; }

    public DateTime Created { get; internal set; }

    public int? ServiceId { get; internal set; }

    public string Name { get; internal set; }
    public string ServiceName { get; internal set; }
}
