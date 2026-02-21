using ResumableFunctions.Handler;
using ResumableFunctions.Handler.Attributes;
using ResumableFunctions.Handler.BaseUse;
using ResumableFunctions.Handler.InOuts;
using ResumableFunctions.Handler.Testing;

namespace Tests;

public partial class SubFunctionsTests
{
    //same function and sub fnction wait the same wait
    [Fact]
    public async Task SameWaitTwiceSubAndParent_Test()
    {
        using var testShell = new TestShell(nameof(SameWaitTwiceSubAndParent_Test), typeof(SameWaitTwiceSubAndParent));
        await testShell.ScanTypes();

        Assert.Empty(await testShell.RoundCheck(0, 0, 0));


        var instance = new SameWaitTwiceSubAndParent();
        instance.Method1("f1");

        //Assert.Empty(await testShell.RoundCheck(1, 2, 0));
        Assert.Equal(1, await testShell.GetWaitsCount(x => x.Status == WaitStatus.Completed));
    }

    public class SameWaitTwiceSubAndParent : ResumableFunctionsContainer
    {
        [ResumableFunctionEntryPoint("FunctionTwoLevels")]
        public async IAsyncEnumerable<Wait> Test()
        {
            int x = 100;
            yield return WaitGroup(new[]
            {
                WaitFunction(SubFunction1("f1")),
                WaitMethod<string, string>(Method1, $"M1")
            });
            await Task.Delay(100);
            if (x != 100)
                throw new Exception("Locals continuation problem.");
        }

        [SubResumableFunction("SubFunction1")]
        public async IAsyncEnumerable<Wait> SubFunction1(string functionInput)
        {
            int x = 10;
            yield return WaitMethod<string, string>(Method1, $"M1-{functionInput}")
                .AfterMatch((_, _) =>
                {
                    if (x != 10)
                        throw new Exception("Closure in sub function problem.");
                    x += 10;
                });

            //yield return Wait<string, string>(Method1, "M1")
            //    .MatchIf((input, _) => input == functionInput);

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