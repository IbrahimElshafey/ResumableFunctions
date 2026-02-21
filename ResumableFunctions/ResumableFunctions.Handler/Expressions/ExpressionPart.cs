using System.Linq.Expressions;

namespace ResumableFunctions.Handler.Expressions;
public partial class MatchExpressionWriter
{
    internal class ExpressionPart
    {
        public ExpressionPart(
            BinaryExpression expression,
            Expression inputOutputPart,
            Expression hasValuePart)
        {
            InputOutputPart = inputOutputPart;
            ValuePart = hasValuePart;
            Expression = expression;
        }

        public Expression InputOutputPart { get; }
        public Expression ValuePart { get; }
        public BinaryExpression Expression { get; }
        public bool IsMandatory { get; internal set; }
    }

}
