﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ResumableFunctions.Handler.Helpers;

namespace ResumableFunctions.Handler.InOuts.Entities;
public class PrivateData : IEntity<long>, IEntityWithUpdate, IOnSaveEntity
{
    public long Id { get; set; }
    public object Value { get; set; }
    public PrivateDataType Type { get; set; }
    public List<WaitEntity> ClosureLinkedWaits { get; set; }
    public List<WaitEntity> LocalsLinkedWaits { get; set; }

    public DateTime Created { get; set; }

    public int? ServiceId { get; set; }

    public DateTime Modified { get; set; }

    public string ConcurrencyToken { get; set; }
    public int FunctionStateId { get; internal set; }

    public T GetProp<T>(string propName)
    {
        switch (Value)
        {
            case JObject jobject:
                return jobject[propName].ToObject<T>();
            case object closureObject:
                return (T)closureObject.GetType().GetField(propName).GetValue(closureObject);
            default: return default;
        }
    }

    public void OnSave()
    {
        FunctionStateId =
            LocalsLinkedWaits?.FirstOrDefault()?.FunctionStateId ??
            ClosureLinkedWaits?.FirstOrDefault()?.FunctionStateId ??
            0;
    }

    internal object AsType(Type closureClass)
    {
        Value = Value is JObject jobject ? jobject.ToObject(closureClass) : Value;
        return Value ?? Activator.CreateInstance(closureClass);
    }
}
