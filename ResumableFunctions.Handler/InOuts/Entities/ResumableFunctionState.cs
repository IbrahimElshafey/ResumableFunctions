﻿
using System.ComponentModel.DataAnnotations.Schema;
using MessagePack;
using ResumableFunctions.Handler.Helpers;

namespace ResumableFunctions.Handler.InOuts.Entities;
public class ResumableFunctionState : IEntity<int>, IEntityWithUpdate, IEntityWithDelete, IOnSaveEntity, IObjectWithLog
{

    [IgnoreMember]
    [NotMapped]
    public List<LogRecord> Logs { get; set; } = new();
    public int Id { get; set; }
    public int? ServiceId { get; set; }
    public string UserDefinedId { get; set; }
    public DateTime Created { get; set; }
    /// <summary>
    /// Serialized class instance that contain the resumable function instance data
    /// </summary>
    [NotMapped]
    public object StateObject { get; set; }
    public byte[] StateObjectValue { get; set; }

    public List<WaitEntity> Waits { get; set; } = new();


    public ResumableFunctionIdentifier ResumableFunctionIdentifier { get; set; }
    public int ResumableFunctionIdentifierId { get; set; }
    public FunctionInstanceStatus Status { get; set; }
    public DateTime Modified { get; set; }
    public string ConcurrencyToken { get; set; }

    public bool IsDeleted { get; set; }
    //public Closures Closures { get; set; } = new();

    public void OnSave()
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