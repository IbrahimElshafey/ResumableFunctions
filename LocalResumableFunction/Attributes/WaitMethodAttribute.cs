﻿using LocalResumableFunction.Helpers;
using LocalResumableFunction.InOuts;
using MethodBoundaryAspect.Fody.Attributes;

namespace LocalResumableFunction.Attributes;

/// <summary>
///     Add this to the method you want to wait to.
/// </summary>
public sealed class WaitMethodAttribute : OnMethodBoundaryAspect
{
    private PushedMethod _pushedMethod;
    public override object TypeId => nameof(WaitMethodAttribute);

    public override void OnEntry(MethodExecutionArgs args)
    {
        args.MethodExecutionTag = false;
        _pushedMethod = new PushedMethod
        {
            MethodData = new MethodData(args.Method)
        };
        if (args.Arguments.Length > 0)
            _pushedMethod.Input = args.Arguments[0];
    }

    public override void OnExit(MethodExecutionArgs args)
    {
        _pushedMethod.Output = args.ReturnValue;
        //var isTaskResult = args.ReturnValue.GetType().GetGenericTypeDefinition() == typeof(Task<>);
        if (Extensions.IsAsyncMethod(args.Method))
        {
            dynamic output = args.ReturnValue;
            _pushedMethod.Output = output.Result;
        }
        //todo: main method must wait until this completes ==> Ue hangfire
        //_ = new ResumableFunctionHandler().MethodCalled(_pushedMethod);
        new ResumableFunctionHandler().MethodCalled(_pushedMethod).Wait();
        args.MethodExecutionTag = true;
    }

    public override void OnException(MethodExecutionArgs args)
    {
        if ((bool)args.MethodExecutionTag)
            return;
        Console.WriteLine("On exception");
    }
}