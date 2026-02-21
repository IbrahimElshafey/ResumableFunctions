using ResumableFunctions.Handler;
using ResumableFunctions.Handler.Attributes;
using ResumableFunctions.Handler.BaseUse;
using ResumableFunctions.Handler.InOuts;
using ResumableFunctions.Handler.Testing;

namespace Tests;

public partial class SubFunctionsTests
{
    [Fact]
    public async Task FunctionAfterFirst_Test()
    {
        using var test = new TestShell(nameof(FunctionAfterFirst_Test), typeof(FunctionAfterFirst));
        await test.ScanTypes();

        var logs = await test.GetLogs();
        Assert.Empty(logs);

        var instance = new FunctionAfterFirst();
        instance.Method2("m2");
        instance.Method3("m3");

        Assert.Empty(await test.RoundCheck(2, 3, 1));
    }

    [Fact]
    public async Task FunctionAtStart_Test()
    {
        using var test = new TestShell(nameof(FunctionAtStart_Test), typeof(SubFunctions));
        await test.ScanTypes();

        var logs = await test.GetLogs();
        Assert.Empty(logs);

        var instance = new SubFunctions();
        instance.Method1("m12");

        var pushedCalls = await test.GetPushedCalls();
        Assert.Single(pushedCalls);
        var instances = await test.GetInstances<SubFunctions>(true);
        Assert.Equal(2, instances.Count);
        Assert.Equal(1, instances.Count(x => x.Status == FunctionInstanceStatus.Completed));
        var waits = await test.GetWaits(null, true);
        Assert.Equal(4, waits.Count);
        Assert.Equal(2, waits.Count(x => x.Status == WaitStatus.Completed));
    }

    [Fact]
    public async Task TwoFunctionsAtFirst_Test()
    {
        using var test = new TestShell(nameof(TwoFunctionsAtFirst_Test), typeof(TwoFunctionsAtFirst));
        await test.ScanTypes();

        var logs = await test.GetLogs();
        Assert.Empty(logs);

        var instance = new TwoFunctionsAtFirst();
        instance.Method1("m1");
        instance.Method2("m2");

        var pushedCalls = await test.GetPushedCalls();
        Assert.Equal(2, pushedCalls.Count);
        var instances = await test.GetInstances<SubFunctions>(true);
        Assert.Equal(2, instances.Count);
        Assert.Equal(1, instances.Count(x => x.Status == FunctionInstanceStatus.Completed));
        var waits = await test.GetWaits(null, true);
        Assert.Equal(10, waits.Count);
        Assert.Equal(5, waits.Count(x => x.Status == WaitStatus.Completed));


        instance.Method1("m3");
        instance.Method2("m4");

        pushedCalls = await test.GetPushedCalls();
        Assert.Equal(4, pushedCalls.Count);
        instances = await test.GetInstances<SubFunctions>(true);
        Assert.Equal(3, instances.Count);
        Assert.Equal(2, instances.Count(x => x.Status == FunctionInstanceStatus.Completed));
        waits = await test.GetWaits(null, true);
        Assert.Equal(15, waits.Count);
        Assert.Equal(10, waits.Count(x => x.Status == WaitStatus.Completed));
    }

    public class TwoFunctionsAtFirst : ResumableFunctionsContainer
    {
        [ResumableFunctionEntryPoint("TwoFunctionsAtFirst")]
        public async IAsyncEnumerable<Wait> Test()
        {
            yield return WaitGroup(new[] 
            {
                 WaitFunction(SubFunction1()),
                 WaitFunction(SubFunction2())
            }, "Wait two sub functions");
            await Task.Delay(100);
        }

        [SubResumableFunction("SubFunction1")]
        public async IAsyncEnumerable<Wait> SubFunction1()
        {
            yield return WaitMethod<string, string>(Method1, "M1").MatchAny();
        }

        [SubResumableFunction("SubFunction2")]
        public async IAsyncEnumerable<Wait> SubFunction2()
        {
            yield return WaitMethod<string, string>(Method2, "M2").MatchAny();
        }

        [PushCall("RequestAdded")] public string Method1(string input) => input + "M1";
        [PushCall("Method2")] public string Method2(string input) => input + "M2";
    }

    public class FunctionAfterFirst : ResumableFunctionsContainer
    {
        public string Message { get; set; }

        [ResumableFunctionEntryPoint("FunctionAfterFirst")]
        public async IAsyncEnumerable<Wait> Test()
        {
            yield return WaitMethod<string, string>(Method2, "M2");
            yield return WaitFunction(SubFunction2(155), "Wait sub function2");
            await Task.Delay(100);
        }
            
        [SubResumableFunction("SubFunction2")]
        public async IAsyncEnumerable<Wait> SubFunction2(int funcInput)
        {
            int x = 100;
            yield return WaitMethod<string, string>(Method3, "M3")
                .MatchAny()
                //.AfterMatch(InstanceCall)
                //.AfterMatch(TestMethodClass.AfterMatchExternal)
                .AfterMatch((input, output) =>
                {
                    Message = $"Input: {input}, Output: {output}";
                    if (x != 100)
                        throw new Exception("Closure not saved for sub resumable function.");
                    funcInput += 5;
                })
                ;
            Console.WriteLine(x);
            if (funcInput != 160)
                throw new Exception("SubResumableFunction input must be 160.");
        }

        private void InstanceCall(string arg1, string arg2)
        {
            Message = $"Input: {arg1}, Output: {arg2}";
        }

        [PushCall("Method2")] public string Method2(string input) => input + "M2";
        [PushCall("Method3")] public string Method3(string input) => input + "M3";
    }
    public static class TestMethodClass
    {
        public static void AfterMatchExternal(string input, string outPut) => Console.WriteLine($"{input}#{outPut}");
    }
    public class SubFunctions : ResumableFunctionsContainer
    {
        [ResumableFunctionEntryPoint("FunctionAtStart")]
        public async IAsyncEnumerable<Wait> FunctionAtStart()
        {
            yield return WaitFunction(SubFunction(), "Wait sub function");
            await Task.Delay(100);
        }

        [SubResumableFunction("SubFunction")]
        public async IAsyncEnumerable<Wait> SubFunction()
        {
            yield return WaitMethod<string, string>(Method1, "M1").MatchAny();
        }

        [PushCall("RequestAdded")] public string Method1(string input) => input + "M1";

    }
}