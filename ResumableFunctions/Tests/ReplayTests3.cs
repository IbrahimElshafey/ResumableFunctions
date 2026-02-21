using ResumableFunctions.Handler;
using ResumableFunctions.Handler.Attributes;
using ResumableFunctions.Handler.BaseUse;
using ResumableFunctions.Handler.Testing;

namespace Tests;
public partial class ReplayTests
{
    [Fact]
    public async Task GoBackToNormalMethodClosure_Test()
    {
        using var test = new TestShell(nameof(GoBackToNormalMethodClosure_Test), typeof(GoBackToNormalMethodClosure));
        await test.ScanTypes();

        Assert.Empty(await test.RoundCheck(0, 0, 0));

        var instance = new GoBackToNormalMethodClosure();
        instance.Method1("M1");
        instance.Method2("M2");
        instance.Method2("M2-Replay");

        Assert.Empty(await test.RoundCheck(3, 3, 1));
    }

    public class GoBackToNormalMethodClosure : ResumableFunctionsContainer
    {
        public int Counter { get; set; }

        [ResumableFunctionEntryPoint("ReplayGoToFunction")]
        public async IAsyncEnumerable<Wait> Test()
        {
            var localCounter = 10;
            yield return WaitMethodOne();

            Counter += 10;

        wait_method_two:
            yield return WaitMethodTwo();

            Counter += 3;
            localCounter += 10;

            if (Counter < 16)
                goto wait_method_two;

            if (localCounter != 30)
                throw new Exception("Locals continuation problem.");
            await Task.Delay(100);
        }

        private MethodWait<string, string> WaitMethodOne()
        {
            int counter = 20;
            return
                WaitMethod<string, string>(Method1, "M1").
                MatchIf((input, _) => input.StartsWith("M")).
                WhenCancel(() => counter += 10);
        }

        private Wait WaitMethodTwo()
        {
            var methodTwoCounter = 10;
            return
                WaitMethod<string, string>(Method2, $"M2_{Random.Shared.Next(1, 100)}")
                 .AfterMatch((_, _) =>
                 {
                     methodTwoCounter += 10;
                     if (methodTwoCounter != 20)
                         throw new Exception("Method closure reused for local method.");
                 })
                 .MatchAny();
        }

        [PushCall("Method1")] public string Method1(string input) => input + "M1";
        [PushCall("Method2")] public string Method2(string input) => input + "M2";
    }
}