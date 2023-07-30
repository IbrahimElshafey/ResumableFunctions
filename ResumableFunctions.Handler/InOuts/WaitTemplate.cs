﻿using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Runtime;
using System.Security.Cryptography;
using System.Text;
using FastExpressionCompiler;
using ResumableFunctions.Handler.Expressions;
using ResumableFunctions.Handler.Helpers;

namespace ResumableFunctions.Handler.InOuts;

public class WaitTemplate : IEntity, IOnSaveEntity
{

    public int Id { get; internal set; }
    public int FunctionId { get; internal set; }
    public int MethodId { get; internal set; }
    public int MethodGroupId { get; internal set; }
    public MethodsGroup MethodGroup { get; internal set; }
    public byte[] Hash { get; internal set; }
    public DateTime Created { get; internal set; }
    public DateTime DeactivationDate { get; internal set; }
    public bool IsMandatoryPartFullMatch { get; internal set; }

    internal string MatchExpressionValue { get; set; }
    public byte[] CancelMethodActionValue { get; internal set; }
    internal string CallMandatoryPartExpressionValue { get; set; }

    internal string InstanceMandatoryPartExpressionValue { get; set; }
    internal byte[] AfterMatchActionValue { get; set; }

    [NotMapped]
    public MethodData CancelMethodAction { get; internal set; }

    [NotMapped]
    public LambdaExpression MatchExpression { get; internal set; }

    [NotMapped]
    public LambdaExpression CallMandatoryPartExpression { get; internal set; }

    [NotMapped]
    public LambdaExpression InstanceMandatoryPartExpression { get; internal set; }



    [NotMapped]
    public MethodData AfterMatchAction { get; internal set; }


    public int? ServiceId { get; set; }
    public int InCodeLine { get; internal set; }
    public int IsActive { get; internal set; } = 1;

    bool expressionsLoaded;
    internal void LoadUnmappedProps(bool forceReload = false)
    {
        try
        {
            var serializer = new ExpressionSerializer();
            var converter = new BinaryToObjectConverter();
            if (expressionsLoaded && !forceReload) return;

            if (MatchExpressionValue != null)
                MatchExpression = (LambdaExpression)serializer.Deserialize(MatchExpressionValue).ToExpression();
            if (AfterMatchActionValue != null)
                AfterMatchAction = converter.ConvertToObject<MethodData>(AfterMatchActionValue);
            if (CallMandatoryPartExpressionValue != null)
                CallMandatoryPartExpression = (LambdaExpression)serializer.Deserialize(CallMandatoryPartExpressionValue).ToExpression();
            if (InstanceMandatoryPartExpressionValue != null)
                InstanceMandatoryPartExpression = (LambdaExpression)serializer.Deserialize(InstanceMandatoryPartExpressionValue).ToExpression();
            if (CancelMethodActionValue != null)
                CancelMethodAction = converter.ConvertToObject<MethodData>(CancelMethodActionValue);

        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        expressionsLoaded = true;
    }

    public static byte[] CalcHash(LambdaExpression matchExpression, LambdaExpression setDataExpression)
    {
        var serializer = new ExpressionSerializer();
        var matchBytes = Encoding.UTF8.GetBytes(serializer.Serialize(matchExpression.ToExpressionSlim()));
        var setDataBytes = Encoding.UTF8.GetBytes(serializer.Serialize(setDataExpression.ToExpressionSlim()));
        using var md5 = MD5.Create();
        var mergedHash = new byte[matchBytes.Length + setDataBytes.Length];
        Array.Copy(matchBytes, mergedHash, matchBytes.Length);
        Array.Copy(setDataBytes, 0, mergedHash, matchBytes.Length, setDataBytes.Length);

        return md5.ComputeHash(mergedHash);
    }

    public void OnSave()
    {
        var serializer = new ExpressionSerializer();
        var converter = new BinaryToObjectConverter();
        if (MatchExpression != null)
            MatchExpressionValue = serializer.Serialize(MatchExpression.ToExpressionSlim());
        if (AfterMatchAction != null)
            AfterMatchActionValue = converter.ConvertToBinary(AfterMatchAction);
        if (CallMandatoryPartExpression != null)
            CallMandatoryPartExpressionValue = serializer.Serialize(CallMandatoryPartExpression.ToExpressionSlim());
        if (InstanceMandatoryPartExpression != null)
            InstanceMandatoryPartExpressionValue = serializer.Serialize(InstanceMandatoryPartExpression.ToExpressionSlim());
        if (CancelMethodAction != null)
            CancelMethodActionValue = converter.ConvertToBinary(CancelMethodAction);
    }

    internal string GetMandatoryPart(byte[] pushedCallDataValue)
    {
        if (CallMandatoryPartExpression != null)
        {
            var inputType = CallMandatoryPartExpression.Parameters[0].Type;
            var outputType = CallMandatoryPartExpression.Parameters[1].Type;
            var methodData = PushedCall.GetMethodData(inputType, outputType, pushedCallDataValue);
            var getMandatoryFunc = CallMandatoryPartExpression.CompileFast();
            var parts = (object[])getMandatoryFunc.DynamicInvoke(methodData.Input, methodData.Output);
            return string.Join("#", parts);
        }
        return null;
    }
}
