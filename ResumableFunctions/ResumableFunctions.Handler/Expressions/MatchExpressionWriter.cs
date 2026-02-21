using FastExpressionCompiler;
using ResumableFunctions.Handler.Helpers;
using ResumableFunctions.Handler.InOuts;
using System.Linq.Expressions;
using static System.Linq.Expressions.Expression;

namespace ResumableFunctions.Handler.Expressions;

/// <summary>
/// Transforms and analyzes match expressions to extract mandatory parts and prepare them for evaluation.
/// Converts lambda expressions with 2 parameters (input, output) to 4 parameters (input, output, functionInstance, closure).
/// </summary>
public partial class MatchExpressionWriter : ExpressionVisitor
{
    private LambdaExpression _matchExpression;
    private readonly object _currentFunctionInstance;
    private readonly List<ExpressionPart> _expressionParts = new();

    /// <summary>
    /// Gets the analyzed and transformed match expression parts.
    /// </summary>
    public MatchExpressionParts MatchExpressionParts { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MatchExpressionWriter"/> class.
    /// Processes the match expression to identify mandatory parts and transform the signature.
    /// </summary>
    /// <param name="matchExpression">The lambda expression to analyze and transform.</param>
    /// <param name="functionInstance">The current function instance for closure resolution.</param>
    public MatchExpressionWriter(LambdaExpression matchExpression, object functionInstance)
    {
        MatchExpressionParts = new();
        _matchExpression = matchExpression;
        
        // Early exit if no expression to process
        if (_matchExpression == null)
            return;
        
        // If already transformed to 4 parameters, use as-is
        if (_matchExpression?.Parameters.Count == 4)
        {
            MatchExpressionParts.MatchExpression = _matchExpression;
            return;
        }
        
        _currentFunctionInstance = functionInstance;

        // Transform the expression through multiple stages
        ChangeSignature();           // Convert from 2 to 4 parameters
        FindExactMatchParts();       // Identify exact match comparisons
        MarkMandatoryParts();        // Determine which parts are mandatory for matching
        ReplaceLocalVariables();     // Replace closure variables with parameter references
        
        // Generate mandatory part expressions if any exist
        if (_expressionParts.Any(x => x.IsMandatory))
        {
            var mandatoryPartVisitor = new MandatoryPartExpressionsGenerator(_matchExpression, _expressionParts);
            MatchExpressionParts.CallMandatoryPartPaths = mandatoryPartVisitor.CallMandatoryPartPaths;
            MatchExpressionParts.InstanceMandatoryPartExpression = mandatoryPartVisitor.InstanceMandatoryPartExpression;
            SetIsMandatoryPartFullMatchValue();
        }
    }

    /// <summary>
    /// Replaces closure variable references with the closure parameter and captures the closure instance.
    /// </summary>
    private void ReplaceLocalVariables()
    {
        var changeClosureVarsVisitor = new GenericVisitor();
        Expression closure = null;
        
        // Find and replace compiler-generated closure constants with the closure parameter
        changeClosureVarsVisitor.OnVisitConstant(node =>
        {
            if (node.Type.Name.StartsWith(Constants.CompilerClosurePrefix))
            {
                closure = node;
                // Replace with the 4th parameter (closure)
                return _matchExpression.Parameters[3];
            }
            return base.VisitConstant(node);
        });
        
        MatchExpressionParts.MatchExpression = (LambdaExpression)changeClosureVarsVisitor.Visit(MatchExpressionParts.MatchExpression);
        
        // Capture the actual closure instance for later use
        if (closure != null)
        {
            var value = Lambda<Func<object>>(closure).CompileFast().Invoke();
            if (value != null)
                MatchExpressionParts.Closure = value;
        }
    }

    /// <summary>
    /// Transforms the expression signature from (input, output) to (input, output, functionInstance, closure).
    /// </summary>
    private void ChangeSignature()
    {
        var expression = _matchExpression;
        
        // Create new parameters with proper types
        var inputArg = Parameter(_matchExpression.Parameters[0].Type, "input");
        var outputArg = Parameter(_matchExpression.Parameters[1].Type, "output");
        var functionInstanceArg = Parameter(_currentFunctionInstance.GetType(), "functionInstance");
        var closureType = GetClosureType();
        var localVarsArg = Parameter(closureType, "closure");
        
        // Visit the expression tree and replace old parameters with new ones
        var changeParameterVisitor = new GenericVisitor();
        changeParameterVisitor.OnVisitConstant(OnVisitConstant);
        changeParameterVisitor.OnVisitParameter(OnVisitParameter);
        var updatedBoy = changeParameterVisitor.Visit(_matchExpression.Body);
        
        // Build the new function type: Func<TInput, TOutput, TInstance, TClosure, bool>
        var functionType = typeof(Func<,,,,>)
            .MakeGenericType(
                inputArg.Type,
                outputArg.Type,
                _currentFunctionInstance.GetType(),
                closureType,
                typeof(bool));

        // Create the new lambda with updated signature
        _matchExpression = Lambda(
            functionType,
            updatedBoy,
            inputArg,
            outputArg,
            functionInstanceArg,
            localVarsArg
            );

        // Replace function instance constants with the function instance parameter
        Expression OnVisitConstant(ConstantExpression node)
        {
            if (node.Value == _currentFunctionInstance)
                return functionInstanceArg;
            return base.VisitConstant(node);
        }
        
        // Replace old parameters with new ones
        Expression OnVisitParameter(ParameterExpression parameter)
        {
            if (parameter == _matchExpression.Parameters[0])
                return inputArg;
            if (parameter == _matchExpression.Parameters[1])
                return outputArg;
            return base.VisitParameter(parameter);
        }
    }

    /// <summary>
    /// Detects the closure type from compiler-generated closure constants in the expression tree.
    /// </summary>
    /// <returns>The closure type if found, otherwise <see cref="object"/>.</returns>
    private Type GetClosureType()
    {
        Type result = null;
        var getClosureTypeVisitor = new GenericVisitor();
        
        getClosureTypeVisitor.OnVisitConstant(OnVisitConstant);
        getClosureTypeVisitor.StopWhen(_ => result != null);
        getClosureTypeVisitor.Visit(_matchExpression);
        
        Expression OnVisitConstant(ConstantExpression node)
        {
            // Look for compiler-generated closure types
            if (node.Type.Name.StartsWith(Constants.CompilerClosurePrefix))
                result = node.Type;
            return base.VisitConstant(node);
        }
        
        return result ?? typeof(object);
    }

    /// <summary>
    /// Identifies exact match parts in the expression.
    /// Exact match parts are equality comparisons where one side uses input/output and the other is a constant value.
    /// Also translates boolean properties to explicit equality comparisons (e.g., input.IsHappy => input.IsHappy == true).
    /// </summary>
    private void FindExactMatchParts()
    {
        MatchExpressionParts.MatchExpression = _matchExpression;
        var constantTranslationVisitor = new GenericVisitor();
        constantTranslationVisitor.OnVisitBinary(VisitBinary);
        _matchExpression = (LambdaExpression)constantTranslationVisitor.Visit(_matchExpression);

        Expression VisitBinary(BinaryExpression node)
        {
            // Only process boolean expressions
            if (node.Type != typeof(bool))
                return base.VisitBinary(node);

            // Look for equality comparisons: value == input/output property
            if (node.NodeType == ExpressionType.Equal)
            {
                if (CanConvertToSimpleString(node.Left) && IsInputOutputExpression(node.Right, out _))
                    _expressionParts.Add(new(node, node.Right, node.Left));
                else if (CanConvertToSimpleString(node.Right) && IsInputOutputExpression(node.Left, out _))
                    _expressionParts.Add(new(node, node.Left, node.Right));
            }

            // Translate boolean properties to explicit comparisons:
            // input.IsHappy => input.IsHappy == true
            // !input.IsHappy => input.IsHappy == false
            if (IsInputOutputBoolean(node, node.Left, out Expression newExpression1))
                return newExpression1;
            if (IsInputOutputBoolean(node, node.Right, out Expression newExpression2))
                return newExpression2;

            return base.VisitBinary(node);
        }

        /// <summary>
        /// Checks if an expression can be converted to a simple string value (constant or computable at compile time).
        /// </summary>
        bool CanConvertToSimpleString(Expression expression)
        {
            // Constants are always convertible
            if (expression is ConstantExpression constantExpression)
                return true;

            // Can't convert if it uses input/output parameters
            var useInOut = IsInputOutputExpression(expression, out int inOutUseCount) || inOutUseCount > 0;
            if (useInOut) return false;

            // Try to evaluate the expression
            var result = GetExpressionValue(expression);
            if (result != null)
                return result.CanConvertToSimpleString();

            throw new NotSupportedException(
                $"Can't use expression [{expression}] in match because it's evaluated to [NULL].");
        }

        /// <summary>
        /// Evaluates an expression at compile time using the function instance.
        /// </summary>
        object GetExpressionValue(Expression expression)
        {
            try
            {
                var functionType = typeof(Func<,>).MakeGenericType(_matchExpression.Parameters[2].Type, typeof(object));
                var getExpValue = Lambda(functionType, Convert(expression, typeof(object)), _matchExpression.Parameters[2]).CompileFast();
                return getExpValue.DynamicInvoke(_currentFunctionInstance);
            }
            catch (Exception ex)
            {
                throw new NotSupportedException(message:
                    $"Can't use expression [{expression}] in match because we can't compute its value.\n" +
                    $"Try to rewrite it in another form.\n" +
                    $"Exception: {ex}");
            }
        }
    }

    /// <summary>
    /// Translates boolean input/output expressions to explicit equality comparisons.
    /// Handles both direct boolean properties (input.IsHappy) and negated ones (!input.IsHappy).
    /// </summary>
    private bool IsInputOutputBoolean(BinaryExpression node, Expression operand, out Expression newExpression)
    {
        newExpression = null;
        
        // Boolean arithmetic operators that should not be translated
        var booleanArthimaticOp = new[]
        {
            ExpressionType.And,
            ExpressionType.Or,
            ExpressionType.ExclusiveOr
        };
        
        var isLeft = node.Left == operand;
        var otherOperand = isLeft ? node.Right : node.Left;
        
        // Check if operand is a boolean input/output expression
        var isInputOutputBoolean =
            operand.Type == typeof(bool) &&
            IsInputOutputExpression(operand, out _) &&
            (operand is ParameterExpression || operand is MemberExpression) &&
            otherOperand is not ConstantExpression &&
            !booleanArthimaticOp.Contains(node.NodeType);
        
        // Translate to: operand == true
        var translatedBoolean = isInputOutputBoolean ? MakeBinary(ExpressionType.Equal, operand, Constant(true)) : null;

        // Handle negated boolean: !input.IsHappy
        if (isInputOutputBoolean is false && operand is UnaryExpression unaryExpression)
        {
            isInputOutputBoolean =
                unaryExpression.NodeType == ExpressionType.Not &&
                IsInputOutputExpression(unaryExpression.Operand, out _) &&
                (unaryExpression.Operand is MemberExpression || unaryExpression.Operand is ParameterExpression);
            
            // Translate to: operand == false
            translatedBoolean = isInputOutputBoolean ? MakeBinary(ExpressionType.Equal, unaryExpression.Operand, Constant(false)) : null;
        }

        // Rebuild the binary expression with the translated boolean
        if (isInputOutputBoolean)
        {
            newExpression =
                isLeft ?
                base.VisitBinary(MakeBinary(node.NodeType, translatedBoolean, otherOperand)) :
                base.VisitBinary(MakeBinary(node.NodeType, otherOperand, translatedBoolean));
        }
        return isInputOutputBoolean;
    }

    /// <summary>
    /// Determines which expression parts are mandatory for the match by testing the expression with each part set to false.
    /// A part is mandatory if the expression evaluates to false when that part is false and all others are true.
    /// </summary>
    private void MarkMandatoryParts()
    {
        Expression expressionToCheck = null;

        var changeToBooleans = new GenericVisitor();
        changeToBooleans.OnVisitBinary(VisitBinary);
        changeToBooleans.OnVisitMethodCall(VisitMethodCall);

        // Test each expression part to see if it's mandatory
        foreach (var expressionPart in _expressionParts)
        {
            if (expressionPart.IsMandatory) continue;

            expressionToCheck = expressionPart.Expression;
            var expression = changeToBooleans.Visit(_matchExpression.Body);
            try
            {
                // Compile and evaluate: if result is false, this part is mandatory
                var compiled = Lambda<Func<bool>>(expression).CompileFast();
                expressionPart.IsMandatory = !compiled();
            }
            catch (Exception)
            {
                // If evaluation fails, assume not mandatory
                expressionPart.IsMandatory = false;
            }
        }

        // Replace the current expression part being tested with false,
        // and all simple logical expressions with true
        Expression VisitBinary(BinaryExpression node)
        {
            if (node == expressionToCheck)
                return Constant(false);
            if (node.Left == expressionToCheck)
                return base.VisitBinary(MakeBinary(node.NodeType, Constant(false), node.Right));
            if (node.Right == expressionToCheck)
                return base.VisitBinary(MakeBinary(node.NodeType, node.Left, Constant(false)));
            else if (IsSimpleLogicalExpression(node))
                return Constant(true);
            return base.VisitBinary(node);
        }
        
        // Replace boolean method calls with true
        Expression VisitMethodCall(MethodCallExpression methodCallExpression)
        {
            if (methodCallExpression.Method.ReturnType == typeof(bool))
                return Constant(true);
            return base.VisitMethodCall(methodCallExpression);
        }
    }

    /// <summary>
    /// Determines if matching only mandatory parts is sufficient for a full match.
    /// Tests the expression with mandatory parts as true and all others as false.
    /// </summary>
    private void SetIsMandatoryPartFullMatchValue()
    {
        var mandatoryExactParts = _expressionParts.Where(x => x.IsMandatory).ToList();

        if (mandatoryExactParts.Count == 0)
        {
            MatchExpressionParts.IsMandatoryPartFullMatch = false;
            return;
        }

        var replacer = new GenericVisitor();

        // Replace mandatory parts with true, all others with false
        replacer.OnVisitBinary(node =>
        {
            if (node.Type != typeof(bool))
                return base.VisitBinary(node);

            return mandatoryExactParts.Any(x => x.Expression == node)
                ? Constant(true)
                : Constant(false);
        });

        replacer.OnVisitMethodCall(node =>
        {
            return node.Method.ReturnType == typeof(bool)
                ? Constant(false)
                : base.VisitMethodCall(node);
        });

        // If expression evaluates to true, mandatory parts alone are sufficient
        var transformedBody = replacer.Visit(_matchExpression.Body);
        var compiled = Lambda<Func<bool>>(transformedBody).CompileFast();
        MatchExpressionParts.IsMandatoryPartFullMatch = compiled();
    }

    /// <summary>
    /// Checks if an expression uses only input/output parameters (no function instance or closure variables).
    /// </summary>
    /// <param name="expression">The expression to check.</param>
    /// <param name="inOutUseCount">Output: the number of times input/output parameters are used.</param>
    /// <returns>True if the expression uses only input/output parameters.</returns>
    private bool IsInputOutputExpression(Expression expression, out int inOutUseCount)
    {
        var checkUseParamter = new GenericVisitor();
        var inputOutputParameterCount = 0;
        var instanceAndClosureCount = 0;
        
        checkUseParamter.OnVisitParameter(param =>
        {
            // Count input/output parameter usage
            if (param == _matchExpression.Parameters[0] || param == _matchExpression.Parameters[1])
                inputOutputParameterCount++;
            // Count function instance and closure parameter usage
            if (param == _matchExpression.Parameters[2] || param == _matchExpression.Parameters[3])
                instanceAndClosureCount++;
            return param;
        });
        
        checkUseParamter.OnVisitConstant(constant =>
        {
            // Count closure constant usage
            if (constant.Type.Name.StartsWith(Constants.CompilerClosurePrefix))
                instanceAndClosureCount++;
            return constant;
        });
        
        checkUseParamter.Visit(expression);
        bool UseInputOutputOnly = inputOutputParameterCount > 0 && instanceAndClosureCount == 0;
        inOutUseCount = inputOutputParameterCount;
        return UseInputOutputOnly;
    }

    /// <summary>
    /// Checks if a binary expression is a simple logical expression (contains exactly one logical operator).
    /// </summary>
    private bool IsSimpleLogicalExpression(BinaryExpression node)
    {
        var _booleanLogicalOps = new ExpressionType[]
           {
                ExpressionType.AndAlso,
                ExpressionType.OrElse,
                ExpressionType.Equal,
                ExpressionType.NotEqual,
                ExpressionType.LessThan,
                ExpressionType.LessThanOrEqual,
                ExpressionType.GreaterThan,
                ExpressionType.GreaterThanOrEqual,
                ExpressionType.And,
                ExpressionType.Or,
                ExpressionType.ExclusiveOr,
           };
        
        var logicalOpsCount = 0;
        var countLogicalOpsVisitor = new GenericVisitor();
        countLogicalOpsVisitor.OnVisitBinary(binaryNode =>
        {
            if (_booleanLogicalOps.Contains(binaryNode.NodeType) && binaryNode.Type == typeof(bool))
                logicalOpsCount++;
            return binaryNode;
        });
        
        countLogicalOpsVisitor.Visit(node);
        return logicalOpsCount == 1 && node.Type == typeof(bool);
    }
}
