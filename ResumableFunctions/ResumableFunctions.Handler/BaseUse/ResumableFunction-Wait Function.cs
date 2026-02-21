using ResumableFunctions.Handler.BaseUse;
using ResumableFunctions.Handler.Helpers;
using ResumableFunctions.Handler.InOuts;
using ResumableFunctions.Handler.InOuts.Entities;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace ResumableFunctions.Handler;

public abstract partial class ResumableFunctionsContainer
{
    protected Wait WaitFunction(
        IAsyncEnumerable<Wait> function,
        string name = null,
        [CallerLineNumber] int inCodeLine = 0,
        [CallerMemberName] string callerName = "")
    {
        var runner = function.GetAsyncEnumerator();
        var runnerName = function.GetAsyncEnumerator().GetType().Name;
        var functionName = Regex.Match(runnerName, "<(.+)>").Groups[1].Value;
        var functionInfo = GetType().GetMethod(functionName, CoreExtensions.DeclaredWithinTypeFlags());
        return new FunctionWaitEntity
        {
            Name = name ?? $"#Wait Function `{functionName}`",
            WaitType = WaitType.FunctionWait,
            FunctionInfo = functionInfo,
            CurrentFunction = this,
            CallerName = callerName,
            InCodeLine = inCodeLine,
            Runner = runner,
            Created = DateTime.UtcNow,
        }.ToWait();
    }
}