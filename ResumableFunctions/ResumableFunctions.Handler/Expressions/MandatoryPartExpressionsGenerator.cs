using FastExpressionCompiler;
using Newtonsoft.Json.Linq;
using ResumableFunctions.Handler.Helpers;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Text.Json;
using static ResumableFunctions.Handler.Expressions.MatchExpressionWriter;
using static System.Linq.Expressions.Expression;

namespace ResumableFunctions.Handler.Expressions
{
    internal class MandatoryPartExpressionsGenerator : ExpressionVisitor
    {
        private readonly List<ExpressionPart> _mandatoryParts;
        private readonly LambdaExpression _matchExpression;

        /// <summary>
        /// Replaces the old CallMandatoryPartPaths (heavy serialized lambda).
        /// Stored in WaitTemplates as plain JSON array: ["output.ProjectId","input.RegionId"]
        /// Order matches InstanceMandatoryPartExpression output order (both sorted by InputOutputPart.ToString())
        /// </summary>
        public List<string> CallMandatoryPartPaths { get; }

        /// <summary>
        /// Unchanged — evaluates instance constants at wait registration time.
        /// Caller must serialize result as JSON array → stored in Waits.MandatoryPart.
        /// e.g. ["42","Paid|Urgent","12"]
        /// </summary>
        public LambdaExpression InstanceMandatoryPartExpression { get; }

        public MandatoryPartExpressionsGenerator(
            LambdaExpression matchExpression,
            List<ExpressionPart> expressionParts)
        {
            _matchExpression = matchExpression;
            _mandatoryParts = expressionParts
                .Where(x => x.IsMandatory)
                .OrderBy(x => x.InputOutputPart.ToString()) // same order as before
                .ToList();

            CallMandatoryPartPaths = ExtractCallPaths();
            InstanceMandatoryPartExpression = GetInstanceMandatoryPartExpression();
        }

        /// <summary>
        /// Walks each InputOutputPart MemberExpression chain to produce a dot-separated path.
        /// output.ProjectId           → "output.ProjectId"
        /// input.Order.RegionId       → "input.Order.RegionId"
        /// output.IsActive (bool)     → "output.IsActive"
        /// </summary>
        private List<string> ExtractCallPaths() =>
            _mandatoryParts
                .Select(x => BuildPath(x.InputOutputPart))
                .ToList();

        private string BuildPath(Expression expression)
        {
            var parts = new Stack<string>();
            var current = expression;

            // Walk MemberExpression chain: output.Order.ProjectId
            while (current is MemberExpression member)
            {
                parts.Push(member.Member.Name);
                current = member.Expression;
            }

            // Root must be input or output parameter
            if (current is ParameterExpression param)
            {
                var prefix = param == _matchExpression.Parameters[0] ? "input" : "output";
                parts.Push(prefix);
            }

            return string.Join(".", parts);
        }

        private LambdaExpression GetInstanceMandatoryPartExpression()
        {
            return Lambda(
                NewArrayInit(
                    typeof(object),
                    _mandatoryParts.Select(x => Convert(ValuePart(x.ValuePart), typeof(object)))),
                _matchExpression.Parameters[2],
                _matchExpression.Parameters[3]);

            Expression ValuePart(Expression valuePart)
            {
                var changeClosureVarsVisitor = new GenericVisitor();
                changeClosureVarsVisitor.OnVisitConstant(node =>
                {
                    if (node.Type.Name.StartsWith(Constants.CompilerClosurePrefix))
                        return _matchExpression.Parameters[3];
                    return base.VisitConstant(node);
                });
                return changeClosureVarsVisitor.Visit(valuePart);
            }
        }
    }

    // -------------------------------------------------------
    // Call-time resolution — replaces executing CallMandatoryPartPaths
    // -------------------------------------------------------

    internal class MandatoryPartResolver
    {
        private static readonly ConcurrentDictionary<string, Func<JObject, JObject, string?>> _cache = new();

        /// <summary>
        /// Builds the MandatoryPart match value from an incoming call.
        /// Replaces: Deserialize(CallMandatoryPartPaths).Compile()(input, output)
        /// Result:   ["42","Paid|Urgent","12"]  — matches Waits.MandatoryPart exactly
        /// </summary>
        public static string BuildFromCall(object input, object output, List<string> paths)
        {
            var inputObj = JObject.FromObject(input);
            var outputObj = JObject.FromObject(output);

            var values = paths.Select(path =>
                _cache.GetOrAdd(path, BuildAccessor)(inputObj, outputObj));

            return JsonSerializer.Serialize(values);
        }

        private static Func<JObject, JObject, string?> BuildAccessor(string path)
        {
            var dot = path.IndexOf('.');
            var prefix = path[..dot];        // "input" or "output"
            var token = path[(dot + 1)..];  // "ProjectId" or "Order.RegionId"

            return (inputObj, outputObj) =>
                (prefix == "output" ? outputObj : inputObj)
                    .SelectToken(token)        // handles nested paths free of charge
                    ?.ToString();
        }
    }

    // -------------------------------------------------------
    // Registration side — serializes instance constants to JSON
    // -------------------------------------------------------

    internal static class MandatoryPartSerializer
    {
        /// <summary>
        /// Executes InstanceMandatoryPartExpression and serializes to JSON array.
        /// Called once at wait registration — result stored in Waits.MandatoryPart.
        /// Never recomputed for the lifetime of that wait row.
        /// </summary>
        public static string Serialize(
            LambdaExpression instanceExpression,
            object functionInstance,
            object closure)
        {
            var values = (object[])instanceExpression
                .CompileFast()
                .DynamicInvoke(functionInstance, closure)!;

            return JsonSerializer.Serialize(
                values.Select(v => v?.ToString()).ToList());
            // ["42","Paid|Urgent","12"] — handles |, #, quotes, any character
        }
    }
}