using ResumableFunctions.Handler.Helpers;
using System.Dynamic;
using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace ResumableFunctions.Handler.Expressions
{
    internal class DynamicMatchVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _inputOutput;
        private readonly ParameterExpression _instance;
        private bool _stop;
        public Expression<Func<ExpandoObject, ExpandoObject, bool>> Result { get; internal set; }

        public DynamicMatchVisitor(LambdaExpression matchExpression)
        {
            _inputOutput = Parameter(typeof(ExpandoObject), "inputOutput");
            _instance = Parameter(typeof(ExpandoObject), "instance");
            var result = Visit(matchExpression.Body);
            if (!_stop && result != null)
                Result = Lambda<Func<ExpandoObject, ExpandoObject, bool>>(result, _inputOutput, _instance);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            var parameter = GetParamter(node.ToString());
            if (CanConvertToString(node.Type))
            {
                var getValueMi = typeof(ExpandoExtensions).GetMethods().First(x => x.Name == "Get" && x.IsGenericMethod).MakeGenericMethod(node.Type);
                return Call(
                    getValueMi,
                    parameter.ParameterExpression,
                    parameter.Path
                );
            }

            _stop = true;
            return base.VisitMember(node);
        }

        private (ParameterExpression ParameterExpression, ConstantExpression Path) GetParamter(string path)
        {
            if (path.StartsWith("input.") || path.StartsWith("output."))
                return (_inputOutput, Constant(path));
            if (path.StartsWith("functionInstance."))
                return (_instance, Constant(path.Substring(17)));
            throw new Exception($"Can't access to [{path}]");
        }

        public override Expression Visit(Expression node)
        {
            if (_stop) return node;
            return base.Visit(node);
        }

        private bool CanConvertToString(Type type)
        {
            return type.IsConstantType() || type == typeof(DateTime) || type == typeof(Guid) || type.IsEnum;
        }
    }
}