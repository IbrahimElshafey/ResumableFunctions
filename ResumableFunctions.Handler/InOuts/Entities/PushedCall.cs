﻿using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Reflection;
using FastExpressionCompiler;
using Newtonsoft.Json;
using ResumableFunctions.Handler.Helpers;

namespace ResumableFunctions.Handler.InOuts.Entities;
public class PushedCall : IEntity<long>, IOnSaveEntity
{
    public long Id { get; set; }
    [NotMapped]
    public MethodData MethodData { get; set; }
    public byte[] MethodDataValue { get; set; }
    [NotMapped]
    public InputOutput Data { get; set; } = new();
    public byte[] DataValue { get; set; }
    public int? ServiceId { get; set; }

    public DateTime Created { get; set; }
    public string MethodUrn { get; set; }

    internal string GetMandatoryPart(LambdaExpression CallMandatoryPartExpression)
    {
        if (CallMandatoryPartExpression != null)
        {
            var inputType = CallMandatoryPartExpression.Parameters[0].Type;
            var outputType = CallMandatoryPartExpression.Parameters[1].Type;
            var methodData = GetMethodData(inputType, outputType, DataValue);
            var getMandatoryFunc = CallMandatoryPartExpression.CompileFast();
            var parts = (object[])getMandatoryFunc.DynamicInvoke(methodData.Input, methodData.Output);
            return string.Join("#", parts);
        }
        return null;
    }

    public void OnSave()
    {
        var converter = new BinarySerializer();
        DataValue = converter.ConvertToBinary(Data);
        MethodDataValue = converter.ConvertToBinary(MethodData);
        MethodUrn = MethodData?.MethodUrn;
    }

    public void LoadUnmappedProps(MethodInfo methodInfo = null)
    {
        var converter = new BinarySerializer();
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

    private static InputOutput GetMethodData(Type inputType, Type outputType, byte[] dataBytes)
    {
        var converter = new BinarySerializer();
        var genericInputOutPut = typeof(GInputOutput<,>).MakeGenericType(inputType, outputType);
        dynamic data = converter.ConvertToObject(dataBytes, genericInputOutPut);
        return InputOutput.FromGeneric(data);
    }

    public override string ToString()
    {
        return JsonConvert.SerializeObject(
            this,
            Formatting.None,
            new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore
            });
    }
}