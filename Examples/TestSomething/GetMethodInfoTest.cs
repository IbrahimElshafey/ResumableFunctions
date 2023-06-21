﻿using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using ResumableFunctions.Handler.Core;
using ResumableFunctions.Handler.Helpers;
using ResumableFunctions.Handler.Helpers.Expressions;

namespace TestSomething;
internal class GetMethodInfoTest
{
    internal void Run()
    {
        //var mi1 = GetMethodInfo<WaitProcessor>(wp => wp.ProcessFunctionExpectedMatches);
        //var mi2 = GetMethodInfo<WaitProcessor>(wp => wp.ProcessFunctionExpectedMatches(1, 1));
        //var mi3 = GetMethodInfo<string>(wp => string.Compare("", "sss"));
        //var mi4 = GetMethodInfo<WaitProcessor>(wp => wp.ToString());
        //var mi5 = GetMethodInfo<List<int>>(wp => wp.Any(x => x == 10));
    }

    public static (MethodInfo MethodInfo, Type OwnerType) GetMethodInfo<T>(Expression<Func<T, object>> methodSelector)
    {
        MethodInfo mi = null;
        Type ownerType = null;
        var visitor = new GenericVisitor();
        visitor.OnVisitCall(VisitMethod);
        visitor.OnVisitConstant(VisitConstant);
        visitor.Visit(methodSelector);
        return (mi, ownerType);

        Expression VisitMethod(MethodCallExpression node)
        {
            if (IsInCurrentType(node.Method))
                mi = node.Method;
            return node;
        }
        Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value is MethodInfo info && IsInCurrentType(info))
                mi = info;
            return node;
        }

        bool IsInCurrentType(MethodInfo methodInfo)
        {
            bool isExtension = methodInfo.IsDefined(typeof(ExtensionAttribute), true);
            if (isExtension)
            {
                var extensionOnType = methodInfo.GetParameters()[0].ParameterType;
                var canBeAppliedToCurrent = extensionOnType.IsAssignableFrom(typeof(T));
                if (canBeAppliedToCurrent)
                {
                    ownerType = extensionOnType;
                    return true;
                }
                else
                {
                    return false;
                }
            }

            bool inCurrentType = methodInfo.ReflectedType.IsAssignableFrom(typeof(T));
            if (inCurrentType)
                ownerType = methodInfo.ReflectedType;
            return inCurrentType;
        }
    }
}