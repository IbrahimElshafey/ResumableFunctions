﻿using System.Reflection;

namespace ResumableFunctions.Handler.InOuts.Entities;

public class ResumableFunctionIdentifier : MethodIdentifier
{
    public string RF_MethodUrn { get; set; }
    public List<WaitEntity> WaitsCreatedByFunction { get; set; }
    public List<ResumableFunctionState> ActiveFunctionsStates { get; set; }
    public bool IsActive { get; set; } = true;


    public bool IsEntryPoint => Type == MethodType.ResumableFunctionEntryPoint;

    private Type _classType;
    public Type InClassType =>
        _classType ??= Assembly.LoadFrom(AppContext.BaseDirectory + AssemblyName).GetType(ClassName);


    internal override void FillFromMethodData(MethodData methodData)
    {
        RF_MethodUrn = methodData.MethodUrn;
        IsActive = methodData.IsActive;
        base.FillFromMethodData(methodData);
    }
}

