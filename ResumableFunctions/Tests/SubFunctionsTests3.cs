using ResumableFunctions.Handler;
using ResumableFunctions.Handler.Attributes;
using ResumableFunctions.Handler.BaseUse;
using ResumableFunctions.Handler.InOuts;
using ResumableFunctions.Handler.Testing;

namespace Tests;

public partial class SubFunctionsTests
{
    [Fact]
    public async Task FunctionLevels_Test()
    {
        using var test = new TestShell(nameof(FunctionLevels_Test), typeof(FunctionLevels));
        await test.ScanTypes();

        var logs = await test.GetLogs();
        Assert.Empty(logs);

        var instance = new FunctionLevels();
        instance.Method1("m1_1");
        instance.Method4("m4_1");
        instance.Method2("m2_1");
        instance.Method3("m3_1");

        logs = await test.GetLogs();
        Assert.Empty(logs);
        var pushedCalls = await test.GetPushedCalls();
        Assert.Equal(4, pushedCalls.Count);
        var instances = await test.GetInstances<FunctionLevels>(true);
        Assert.Equal(2, instances.Count);
        Assert.Equal(1, instances.Count(x => x.Status == FunctionInstanceStatus.Completed));
        var waits = await test.GetWaits();
        Assert.Equal(7, waits.Count);
        Assert.Equal(7, waits.Count(x => x.Status == WaitStatus.Completed));


        instance.Method1("m1_2");
        instance.Method4("m4_2");
        instance.Method2("m2_2");
        instance.Method3("m3_2");

        logs = await test.GetLogs();
        Assert.Empty(logs);
        pushedCalls = await test.GetPushedCalls();
        Assert.Equal(8, pushedCalls.Count);
        instances = await test.GetInstances<FunctionLevels>(true);
        Assert.Equal(3, instances.Count);
        Assert.Equal(2, instances.Count(x => x.Status == FunctionInstanceStatus.Completed));
        waits = await test.GetWaits();
        Assert.Equal(14, waits.Count);
        Assert.Equal(14, waits.Count(x => x.Status == WaitStatus.Completed));
    }

    public class FunctionLevels : ResumableFunctionsContainer
    {
        [ResumableFunctionEntryPoint("FunctionTwoLevels")]
        public async IAsyncEnumerable<Wait> Test()
        {
            int x = 100;
            yield return WaitFunction(SubFunction1(), "Wait sub function1");
            await Task.Delay(100);
            if (x != 100)
                throw new Exception("Locals continuation problem.");
        }

        [SubResumableFunction("SubFunction1")]
        public async IAsyncEnumerable<Wait> SubFunction1()
        {
            int x = 10;
            yield return WaitMethod<string, string>(Method1, "M1")
                .MatchAny()
                .AfterMatch((_, _) =>
                {
                    if (x != 10)
                        throw new Exception("Closure in sub function problem.");
                    x += 10;
                });

            x += 10;
            yield return WaitMethod<string, string>(Method4, "M4")
                .MatchAny()
                .AfterMatch((_, _) =>
                {
                    if (x != 30)
                        throw new Exception("Closure restore in sub function problem.");
                });
            yield return WaitFunction(SubFunction2(), "Wait sub function2");
        }

        [SubResumableFunction("SubFunction2")]
        public async IAsyncEnumerable<Wait> SubFunction2()
        {
            int x = 100;
            yield return WaitMethod<string, string>(Method2, "M2").MatchAny();
            if (x != 100)
                throw new Exception("Locals continuation problem.");
            x += 100;
            yield return WaitFunction(SubFunction3(), "Wait sub function3");
            if (x != 200)
                throw new Exception("Locals continuation problem.");
        }

        [SubResumableFunction("SubFunction3")]
        public async IAsyncEnumerable<Wait> SubFunction3()
        {
            int x = 1000;
            yield return WaitMethod<string, string>(Method3, "M2").MatchAny();
            if (x != 1000)
                throw new Exception("Locals continuation problem.");
        }

        [PushCall("RequestAdded")] public string Method1(string input) => input + "M1";
        [PushCall("Method2")] public string Method2(string input) => input + "M2";
        [PushCall("Method3")] public string Method3(string input) => input + "M3";
        [PushCall("Method4")] public string Method4(string input) => input + "M4";
    }
}