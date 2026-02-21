using Nuqleon.Json.Serialization;
using System.Linq.Expressions.Bonsai.Serialization;
using Json = Nuqleon.Json.Expressions;
namespace ResumableFunctions.Handler.Expressions
{
    public sealed class ExpressionSerializer : BonsaiExpressionSerializer
    {
        protected override Func<object, Json.Expression> GetConstantSerializer(Type type)
        {
            // REVIEW: Nuqleon.Json has an odd asymmetry in Serialize and Deserialize signatures,
            //         due to the inability to overload by return type. However, it seems odd we
            //         have to go serialize string and subsequently parse into Expression.
            try
            {
                return o => Json.Expression.Parse(new JsonSerializer(type).Serialize(o), ensureTopLevelObjectOrArray: false);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
            }
        }

        protected override Func<Json.Expression, object> GetConstantDeserializer(Type type)
        {
            return json => new JsonSerializer(type).Deserialize(json);
        }
    }
}