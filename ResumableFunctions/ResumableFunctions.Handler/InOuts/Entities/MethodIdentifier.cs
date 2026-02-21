using ResumableFunctions.Handler.Helpers;
using ResumableFunctions.Handler.InOuts.Entities.EntityBehaviour;
using System.Reflection;

namespace ResumableFunctions.Handler.InOuts.Entities;

public abstract class MethodIdentifier : IEntity<int>, IEntityWithUpdate
{
    private MethodInfo _methodInfo;

    public int Id { get; internal set; }
    public string AssemblyName { get; internal set; }
    public DateTime Created { get; internal set; }
    public string ClassName { get; internal set; }
    public string MethodName { get; internal set; }
    public string MethodSignature { get; internal set; }
    public byte[] MethodHash { get; internal set; }
    public MethodType Type { get; internal set; }
    internal MethodInfo MethodInfo
    {
        get
        {
            if (_methodInfo == null)
                _methodInfo = CoreExtensions.GetMethodInfo(AssemblyName, ClassName, MethodName, MethodSignature);
            return _methodInfo;
        }
    }
    public int? ServiceId { get; internal set; }

    public DateTime Modified { get; internal set; }
    public string ConcurrencyToken { get; internal set; }

    public override string ToString()
    {
        return $"AssemblyName:{AssemblyName}, ClassName:{ClassName}, MethodName:{MethodName}, MethodSignature:{MethodSignature}";
    }

    internal virtual void FillFromMethodData(MethodData methodData)
    {
        if (methodData == null) return;
        Type = methodData.MethodType;
        AssemblyName = methodData.AssemblyName;
        ClassName = methodData.ClassName;
        MethodName = methodData.MethodName;
        MethodSignature = methodData.MethodSignature;
        MethodHash = methodData.MethodHash;
    }
}

