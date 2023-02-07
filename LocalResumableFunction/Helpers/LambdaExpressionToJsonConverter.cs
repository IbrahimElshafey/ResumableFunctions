﻿using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Linq.Expressions;

namespace LocalResumableFunction.Helpers
{
    public class LambdaExpressionToJsonConverter : ValueConverter<LambdaExpression, string>
    {
        public LambdaExpressionToJsonConverter()
           : base(
               expression => ExpressionToJsonConverter.ExpressionToJson(expression),
               Json => (LambdaExpression)ExpressionToJsonConverter.JsonToExpression(Json))
        {
        }
    }
}
