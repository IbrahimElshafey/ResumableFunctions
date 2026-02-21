using ResumableFunctions.Handler.Expressions;
using ResumableFunctions.Handler.InOuts.Entities.EntityBehaviour;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;

namespace ResumableFunctions.Handler.InOuts.Entities;

public class WaitTemplate : IEntity<int>, IBeforeSaveEntity
{
    public int Id { get; internal set; }
    public int FunctionId { get; internal set; }
    public int? MethodId { get; internal set; }
    public int MethodGroupId { get; internal set; }
    public MethodsGroup MethodGroup { get; internal set; }
    public byte[] Hash { get; internal set; }
    public DateTime Created { get; internal set; }
    public DateTime DeactivationDate { get; internal set; }
    public bool IsMandatoryPartFullMatch { get; internal set; }

    internal string MatchExpressionValue { get; set; }

    public string CancelMethodAction { get; internal set; }

    [NotMapped]
    public LambdaExpression MatchExpression { get; internal set; }

    public List<string> CallMandatoryPartPaths { get; internal set; }

    [NotMapped]
    public LambdaExpression InstanceMandatoryPartExpression { get; internal set; }
    internal string InstanceMandatoryPartExpressionValue { get; set; }



    public string AfterMatchAction { get; internal set; }


    public int? ServiceId { get; internal set; }
    public int InCodeLine { get; internal set; }
    public int IsActive { get; internal set; } = 1;
    //public List<MethodWaitEntity> Waits { get; internal set; }

    bool expressionsLoaded;
    internal void LoadUnmappedProps(bool forceReload = false)
    {
        try
        {
            var serializer = new ExpressionSerializer();
            if (expressionsLoaded && !forceReload) return;

            if (MatchExpressionValue != null)
                MatchExpression = (LambdaExpression)serializer.Deserialize(MatchExpressionValue).ToExpression();
            //if (CallMandatoryPartPaths != null)
            //    CallMandatoryPartExpression = (LambdaExpression)serializer.Deserialize(CallMandatoryPartPaths).ToExpression();
            if (InstanceMandatoryPartExpressionValue != null)
                InstanceMandatoryPartExpression = (LambdaExpression)serializer.Deserialize(InstanceMandatoryPartExpressionValue).ToExpression();

        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        expressionsLoaded = true;
    }

    internal static byte[] CalcHash(LambdaExpression matchExpression, LambdaExpression setDataExpression)
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

    public void BeforeSave()
    {
        var serializer = new ExpressionSerializer();
        if (MatchExpression != null)
            MatchExpressionValue = serializer.Serialize(MatchExpression.ToExpressionSlim());
        //if (CallMandatoryPartExpression != null)
        //    CallMandatoryPartPaths = serializer.Serialize(CallMandatoryPartExpression.ToExpressionSlim());
        if (InstanceMandatoryPartExpression != null)
            InstanceMandatoryPartExpressionValue = serializer.Serialize(InstanceMandatoryPartExpression.ToExpressionSlim());
    }
}
