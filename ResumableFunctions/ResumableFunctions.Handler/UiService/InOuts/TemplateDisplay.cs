using ResumableFunctions.Handler.Expressions;
using ResumableFunctions.Handler.InOuts.Entities;

namespace ResumableFunctions.Handler.UiService.InOuts
{
    public class TemplateDisplay
    {
        private readonly ExpressionSerializer _serializer;

        public string MatchExpression { get; }
        public string MandatoryPartExpression { get; }

        public TemplateDisplay(WaitTemplate waitTemplate) :
            this(waitTemplate.MatchExpressionValue, waitTemplate.InstanceMandatoryPartExpressionValue)
        {
        }

        public TemplateDisplay(string matchExpressionValue, List<string> callMandatoryPartPaths)
        {
            _serializer = new ExpressionSerializer();
            MatchExpression = GetMatch(matchExpressionValue);
            //MandatoryPartExpression = List<string> callMandatoryPartPaths joined as string
            if (callMandatoryPartPaths?.Any() == true)
            {
                MandatoryPartExpression = string.Join(',', callMandatoryPartPaths);
            }
        }

        public TemplateDisplay(string matchExpressionValue, string instanceMandatoryPartExpressionValue)
        {
            _serializer = new ExpressionSerializer();
            MatchExpression = GetMatch(matchExpressionValue);
            MandatoryPartExpression = GetMandatoryParts(instanceMandatoryPartExpressionValue);
        }

        string GetMatch(string matchExpressionValue)
        {
            if (matchExpressionValue == null) return string.Empty;
            var result = _serializer.Deserialize(matchExpressionValue).ToCSharpString();
            result = result.Split("=>")[1];
            return result;
        }

        string GetMandatoryParts(string instanceMandatoryPartExpressionValue)
        {
            if (instanceMandatoryPartExpressionValue == null) return string.Empty;
            var result = _serializer.Deserialize(instanceMandatoryPartExpressionValue).ToCSharpString();
            result = result.Replace("new object[]", "");
            result = result.Split("=>")[1];
            result = result.Replace("(object)", "");
            return result;
        }
    }
}
