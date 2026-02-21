using ResumableFunctions.Handler;
using ResumableFunctions.Handler.Attributes;
using ResumableFunctions.Handler.BaseUse;
using ResumableFunctions.Handler.Testing;

namespace Tests;

public partial class SubFunctionsTests
{
    [Fact]
    public async Task SameSubFunctionTwice_Test()
    {
        using var test = new TestShell(nameof(SameSubFunctionTwice_Test), typeof(SameSubFunctionTwice));
        await test.ScanTypes();

        var logs = await test.GetLogs();
        Assert.Empty(logs);

        var instance = new SameSubFunctionTwice();
        instance.Method1("f1");
        instance.Method1("f1");

        instance.Method1("f2");
        instance.Method1("f2");

        instance.Method2("f1");
        instance.Method2("f2");

        Assert.Empty(await test.RoundCheck(6, 9, 1));
    }

    public class SameSubFunctionTwice : ResumableFunctionsContainer
    {
        [ResumableFunctionEntryPoint("FunctionTwoLevels")]
        public async IAsyncEnumerable<Wait> Test()
        {
            int x = 100;
            yield return WaitGroup(new[] 
            {
                 WaitFunction(SubFunction1("f1")),
                 WaitFunction(SubFunction1("f2")) 
            }, "Wait sub function1 twice");
            await Task.Delay(100);
            if (x != 100)
                throw new Exception("Locals continuation problem.");
        }

        [SubResumableFunction("SubFunction1")]
        public async IAsyncEnumerable<Wait> SubFunction1(string functionInput)
        {
            int x = 10;
            yield return WaitMethod<string, string>(Method1, $"M1-{functionInput}")
                .MatchIf((input, _) => input == functionInput)
                .AfterMatch((_, _) =>
                {
                    if (x != 10)
                        throw new Exception("Closure in sub function problem.");
                    x += 10;
                });

            yield return WaitMethod<string, string>(Method1, "M1")
                .MatchIf((input, _) => input == functionInput);

            x += 10;
            yield return WaitMethod<string, string>(Method2, "M2")
                .MatchIf((input, _) => input == functionInput)
                .AfterMatch((_, _) =>
                {
                    if (x != 30)
                        throw new Exception("Closure restore in sub function problem.");
                    x += 10;
                });
            if (x != 40)
                throw new Exception("Closure restore in sub function problem.");
        }



        [PushCall("Method1")] public string Method1(string input) => input + "M1";
        [PushCall("Method2")] public string Method2(string input) => input + "M2";
        //[PushCall("Method3")] public string Method3(string input) => input + "M3";
        //[PushCall("Method4")] public string Method4(string input) => input + "M4";
    }
}