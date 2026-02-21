using MessagePack;
using ResumableFunctions.Handler.InOuts.Entities.EntityBehaviour;
using System.ComponentModel.DataAnnotations.Schema;

namespace ResumableFunctions.Handler.InOuts.Entities;
public class ServiceData : IEntity<int>, IObjectWithLog, IEntityWithUpdate
{
    [IgnoreMember]
    [NotMapped]
    public List<LogRecord> Logs { get; set; } = new();
    public int Id { get; internal set; }
    public DateTime Created { get; internal set; }
    public int? ServiceId { get; internal set; }
    public string AssemblyName { get; internal set; }
    public string Url { get; internal set; }

    [NotMapped]
    public string[] ReferencedDlls { get; internal set; }
    public DateTime Modified { get; internal set; }
    public int ParentId { get; internal set; }
    public string ConcurrencyToken { get; internal set; }

    public EntityType EntityType => EntityType.ServiceLog;

    public void AddError(string message, int code, Exception ex = null)
    {
        (this as IObjectWithLog).AddError(message, code, ex);
        Logs.Last().EntityId = ParentId == -1 ? Id : ParentId;
    }

    public void AddLog(string message, LogType logType, int code)
    {
        (this as IObjectWithLog).AddLog(message, logType, code);
        Logs.Last().EntityId = ParentId == -1 ? Id : ParentId;
    }
}
