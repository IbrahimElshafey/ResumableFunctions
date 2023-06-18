﻿using System.Linq.Expressions.Bonsai.Serialization;

namespace TestSomething;

public sealed class ExpressionSerializer : BonsaiExpressionSerializer
{
    protected override Func<object, Nuqleon.Json.Expressions.Expression> GetConstantSerializer(Type type)
    {
        // REVIEW: Nuqleon.Json has an odd asymmetry in Serialize and Deserialize signatures,
        //         due to the inability to overload by return type. However, it seems odd we
        //         have to go serialize string and subsequently parse into Expression.

        return o => Nuqleon.Json.Expressions.Expression.Parse(new Nuqleon.Json.Serialization.JsonSerializer(type).Serialize(o), ensureTopLevelObjectOrArray: false);
    }

    protected override Func<Nuqleon.Json.Expressions.Expression, object> GetConstantDeserializer(Type type)
    {
        return json => new Nuqleon.Json.Serialization.JsonSerializer(type).Deserialize(json);
    }
}