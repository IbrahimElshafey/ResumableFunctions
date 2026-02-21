
using MessagePack;
using ResumableFunctions.Handler.Helpers;
using ResumableFunctions.Handler.InOuts.Entities.EntityBehaviour;
using System.ComponentModel.DataAnnotations.Schema;

namespace ResumableFunctions.Handler.InOuts.Entities;
public class ResumableFunctionState : IEntity<int>, IEntityWithUpdate, IEntityWithDelete, IBeforeSaveEntity, IObjectWithLog
{
    public ResumableFunctionState()
    {

    }
    [IgnoreMember]
    [NotMapped]
    public List<LogRecord> Logs { get; set; } = new();
    public int Id { get; internal set; }
    public int? ServiceId { get; internal set; }
    public DateTime Created { get; internal set; }
    /// <summary>
    /// Serialized class instance that contain the resumable function instance data
    /// </summary>
    [NotMapped]
    public object StateObject { get; internal set; }
    public byte[] StateObjectValue { get; internal set; }

    public List<WaitEntity> Waits { get; internal set; } = new();


    public ResumableFunctionIdentifier ResumableFunctionIdentifier { get; internal set; }
    public int ResumableFunctionIdentifierId { get; internal set; }
    public FunctionInstanceStatus Status { get; internal set; }
    public DateTime Modified { get; internal set; }
    public string ConcurrencyToken { get; internal set; }

    public bool IsDeleted { get; internal set; }

    public EntityType EntityType => EntityType.FunctionInstanceLog;

    //public Closures Closures { get; internal set; } = new();

    public void BeforeSave()
    {
        var converter = new BinarySerializer();
        StateObjectValue = converter.ConvertToBinary(StateObject);
        //foreach (var wait in Waits)
        //{
        //    if (wait is MethodWait mw && mw.Closure != null)
        //    {
        //        Closures[mw.RequestedByFunctionId] = mw.Closure;
        //    }
        //}
    }

    public void LoadUnmappedProps(Type stateObjectType)
    {
        var converter = new BinarySerializer();
        StateObject =
            stateObjectType != null ?
                converter.ConvertToObject(StateObjectValue, stateObjectType) :
                null;
    }
}
