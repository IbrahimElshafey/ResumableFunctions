﻿using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using ResumableFunctions.Handler.Helpers.Expressions;

namespace ResumableFunctions.Handler.InOuts;

public class WaitTemplate : IEntity, IOnSaveEntity
{

    public int Id { get; internal set; }
    public int FunctionId { get; internal set; }
    public int MethodId { get; internal set; }
    public int MethodGroupId { get; internal set; }
    public MethodsGroup MethodGroup { get; internal set; }
    public byte[] BaseHash { get; internal set; }
    public DateTime Created { get; internal set; }
    public bool IsMandatoryPartFullMatch { get; internal set; }

    internal string MatchExpressionValue { get; set; }

    internal string CallMandatoryPartExpressionValue { get; set; }

    internal string InstanceMandatoryPartExpressionValue { get;  set; }
    internal string SetDataExpressionValue { get;  set; }


    public static Expression<Func<WaitTemplate, WaitTemplate>> BasicMatchSelector =>
        waitTemplate => new WaitTemplate
        {
            MatchExpressionValue = waitTemplate.MatchExpressionValue,
            SetDataExpressionValue = waitTemplate.SetDataExpressionValue,
            Id = waitTemplate.Id,
            FunctionId = waitTemplate.FunctionId,
            MethodId = waitTemplate.MethodId,
            MethodGroupId = waitTemplate.MethodGroupId,
            ServiceId = waitTemplate.ServiceId
        };

    public static Expression<Func<WaitTemplate, WaitTemplate>> CallMandatoryPartSelector =>
        waitTemplate => new WaitTemplate
        {
            CallMandatoryPartExpressionValue = waitTemplate.CallMandatoryPartExpressionValue,
            Id = waitTemplate.Id,
            FunctionId = waitTemplate.FunctionId,
            MethodId = waitTemplate.MethodId,
            MethodGroupId = waitTemplate.MethodGroupId,
            IsMandatoryPartFullMatch = waitTemplate.IsMandatoryPartFullMatch,
            ServiceId = waitTemplate.ServiceId
        };
    public static Expression<Func<WaitTemplate, WaitTemplate>> InstanceMandatoryPartSelector =>
        waitTemplate => new WaitTemplate
        {
            InstanceMandatoryPartExpressionValue = waitTemplate.InstanceMandatoryPartExpressionValue,
            Id = waitTemplate.Id,
            FunctionId = waitTemplate.FunctionId,
            MethodId = waitTemplate.MethodId,
            MethodGroupId = waitTemplate.MethodGroupId,
            IsMandatoryPartFullMatch = waitTemplate.IsMandatoryPartFullMatch,
            ServiceId = waitTemplate.ServiceId,
            BaseHash = waitTemplate.BaseHash
        };

    [NotMapped]
    public LambdaExpression MatchExpression { get; internal set; }


    //[NotMapped]
    //public Expression<Func<ExpandoObject, string[]>> CallMandatoryPartExpressionDynamic { get; internal set; }

    [NotMapped]
    public LambdaExpression CallMandatoryPartExpression { get; internal set; }

    [NotMapped]
    public LambdaExpression InstanceMandatoryPartExpression { get; internal set; }



    [NotMapped]
    public LambdaExpression SetDataExpression { get; internal set; }

    [NotMapped]
    public LambdaExpression SetDataExpressionDynamic { get; internal set; }

    public int? ServiceId { get; set; }


    bool expressionsLoaded;
    internal void LoadExpressions(bool forceReload = false)
    {
        var serializer = new ExpressionSerializer();
        if (expressionsLoaded && !forceReload) return;

        if (MatchExpressionValue != null)
            MatchExpression = (LambdaExpression)serializer.Deserialize(MatchExpressionValue).ToExpression();
        if (SetDataExpressionValue != null)
            SetDataExpression = (LambdaExpression)serializer.Deserialize(SetDataExpressionValue).ToExpression();
        if (CallMandatoryPartExpressionValue != null)
            CallMandatoryPartExpression = (LambdaExpression)serializer.Deserialize(CallMandatoryPartExpressionValue).ToExpression();
        if (InstanceMandatoryPartExpressionValue != null)
            InstanceMandatoryPartExpression = (LambdaExpression)serializer.Deserialize(InstanceMandatoryPartExpressionValue).ToExpression();
        //if (_callMandatoryPartExpressionDynamic != null)
        //    CallMandatoryPartExpressionDynamic = (Expression<Func<ExpandoObject, string[]>>)
        //        serializer.Deserialize(_callMandatoryPartExpressionDynamic).ToExpression();

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
        if (MatchExpression != null)
            MatchExpressionValue = serializer.Serialize(MatchExpression.ToExpressionSlim());
        if (SetDataExpression != null)
            SetDataExpressionValue = serializer.Serialize(SetDataExpression.ToExpressionSlim());
        //if (CallMandatoryPartExpressionDynamic != null)
        //    _callMandatoryPartExpressionDynamic = serializer.Serialize(CallMandatoryPartExpressionDynamic.ToExpressionSlim());
        if (CallMandatoryPartExpression != null)
        {
            CallMandatoryPartExpressionValue = serializer.Serialize(CallMandatoryPartExpression.ToExpressionSlim());
            //using var md5 = MD5.Create();
            //MandatoryPartHash = md5.ComputeHash(Encoding.UTF8.GetBytes(CallMandatoryPartExpressionValue));
        }
        if (InstanceMandatoryPartExpression != null)
            InstanceMandatoryPartExpressionValue = serializer.Serialize(InstanceMandatoryPartExpression.ToExpressionSlim());
    }
}