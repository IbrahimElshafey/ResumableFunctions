using FastExpressionCompiler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Text.Json;

namespace ResumableFunctions.Handler.InOuts;

public class MatchExpressionParts
{
    public LambdaExpression MatchExpression { get; internal set; }
    public List<string> CallMandatoryPartPaths { get; internal set; }
    public LambdaExpression InstanceMandatoryPartExpression { get; internal set; }
    public bool IsMandatoryPartFullMatch { get; internal set; }
    public object Closure { get; internal set; }

    private static readonly ConcurrentDictionary<string, Func<JObject, JObject, string?>> _accessorCache = new();

    public string GetInstanceMandatoryPart(object currentFunction)
    {
        if (InstanceMandatoryPartExpression == null) return null;

        var parts = (object[])InstanceMandatoryPartExpression
            .CompileFast()
            .DynamicInvoke(currentFunction, Closure);

        if (parts?.Any() != true) return null;

        return JsonConvert.SerializeObject(
            parts.Select(v => v?.ToString()).ToList());
        // ["42","Paid|Urgent","12"] — handles any character safely
    }

    public string GetPushedCallMandatoryPart(object input, object output)
    {
        if (CallMandatoryPartPaths?.Any() != true) return null;

        var inputObj = JObject.FromObject(input);
        var outputObj = JObject.FromObject(output);

        var values = CallMandatoryPartPaths.Select(path =>
            _accessorCache.GetOrAdd(path, BuildAccessor)(inputObj, outputObj));

        return JsonConvert.SerializeObject(values);
        // Same JSON array format as GetInstanceMandatoryPart → equality match works
    }

    private static Func<JObject, JObject, string?> BuildAccessor(string path)
    {
        var dot = path.IndexOf('.');
        var prefix = path[..dot];       // "input" or "output"
        var token = path[(dot + 1)..]; // "ProjectId" or "Order.RegionId"

        return (inputObj, outputObj) =>
            (prefix == "output" ? outputObj : inputObj)
                .SelectToken(token)
                ?.ToString();
    }
}