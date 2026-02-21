using ResumableFunctions.Handler.InOuts.Entities.EntityBehaviour;

namespace ResumableFunctions.Handler.InOuts.Entities;

public class MethodsGroup : IEntity<int>
{
    internal MethodsGroup()
    {

    }
    public int Id { get; internal set; }
    public string MethodGroupUrn { get; internal set; }
    public bool CanPublishFromExternal { get; internal set; }

    /// <summary>
    /// Method must be handled by it's service only, like TimeWait method will not be redirected to other services.
    /// </summary>
    public bool IsLocalOnly { get; internal set; }
    public List<WaitMethodIdentifier> WaitMethodIdentifiers { get; internal set; } = new();
    public List<MethodWaitEntity> WaitRequestsForGroup { get; internal set; }

    public DateTime Created { get; internal set; }
    public int? ServiceId { get; internal set; }
    public List<WaitTemplate> WaitTemplates { get; internal set; }
}

