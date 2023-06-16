﻿using ResumableFunctions.Handler.Helpers;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace ResumableFunctions.Handler.InOuts;
public class PushedCall : IEntityWithDelete, IOnSaveEntity
{
    public int Id { get; internal set; }
    [NotMapped]
    public MethodData MethodData { get; internal set; }
    public byte[] MethodDataValue { get; internal set; }
    [NotMapped]
    public InputOutput Data { get; internal set; } = new();
    public byte[] DataValue { get; internal set; }
    public int? ServiceId { get; set; }
    public List<WaitForCall> WaitsForCall { get; internal set; } = new();

    public DateTime Created { get; internal set; }

    public bool IsDeleted { get; internal set; }

    public void OnSave()
    {
        var converter = new BinaryToObjectConverter();
        DataValue = converter.ConvertToBinary(Data);
        MethodDataValue = converter.ConvertToBinary(MethodData);
    }

    public void LoadUnmappedProps(MethodInfo methodInfo = null)
    {
        var converter = new BinaryToObjectConverter();
        if (methodInfo == null)
            Data = converter.ConvertToObject<InputOutput>(DataValue);
        else
        {
            var inputType = methodInfo.GetParameters()[0].ParameterType;
            var outputType = methodInfo.IsAsyncMethod() ?
                methodInfo.ReturnType.GetGenericArguments()[0] :
                methodInfo.ReturnType;
            Data = GetMethodData(inputType, outputType, DataValue);
        }
        MethodData = converter.ConvertToObject<MethodData>(MethodDataValue);
    }

    public static InputOutput GetMethodData(Type inputType, Type outputType, byte[] dataBytes)
    {
        var converter = new BinaryToObjectConverter();
        var genericInputOutPut = typeof(GInputOutput<,>).MakeGenericType(inputType, outputType);
        dynamic data = converter.ConvertToObject(dataBytes, genericInputOutPut);
        return InputOutput.FromGeneric(data);
    }
}