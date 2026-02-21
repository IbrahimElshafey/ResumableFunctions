using ResumableFunctions.Handler;
using ResumableFunctions.Handler.Attributes;
using ResumableFunctions.Handler.BaseUse;
using ResumableFunctions.Handler.InOuts;
using ResumableFunctions.Handler.Testing;

namespace Tests;
public class CallerSameNameSequence
{
    [Fact]
    public async Task CallerSameNameSequence_Test()
    {
        using var test = new TestShell(nameof(CallerSameNameSequence_Test), typeof(Test));
        await test.ScanTypes();
        Assert.Empty(await test.RoundCheck(0, 0, 0));

        var function = new Test();
        function.Method1("in1");
        Assert.Empty(await test.RoundCheck(1, 2, 0));

        function.Method2("in2");
        Assert.Empty(await test.RoundCheck(2, 3, 0));

        function.Method3("in3");
        Assert.Empty(await test.RoundCheck(3, 3, 1));
    }


    public class Test : ResumableFunctionsContainer
    {
        [ResumableFunctionEntryPoint("CallerSameNameSequence")]
        public async IAsyncEnumerable<Wait> CallerSameNameSequence()
        {
            yield return CallerSameName(8, 10);

            yield return CallerSameName("string input");

            yield return WaitMethod<string, string>(Method3, "M3").MatchAny();

            await Task.Delay(100);
        }
        private Wait CallerSameName(int x, int y)
        {
            return WaitMethod<string, string>(Method1, "M1")
                .AfterMatch((_, _) => x++);
        }
        private Wait CallerSameName(string input)
        {
            return WaitMethod<string, string>(Method2, "M2").
                MatchAny().
                AfterMatch((_, _) =>
                {
                    if (input != "string input")
                        throw new Exception("closure restore failed.");
                });
        }
        [PushCall("RequestAdded")] public string Method1(string input) => input + "M1";
        [PushCall("Method2")] public string Method2(string input) => input + "M2";
        [PushCall("Method3")] public string Method3(string input) => input + "M3";
    }
}