using ResumableFunctions.Handler;
using ResumableFunctions.Handler.Attributes;
using ResumableFunctions.Handler.BaseUse;
using ResumableFunctions.Handler.InOuts;
using ResumableFunctions.Handler.Testing;

namespace Tests;
public partial class ReplayTests
{
    [Fact]
    public async Task ReplayInSubFunction_Test()
    {
        using var test = new TestShell(nameof(ReplayInSubFunction_Test), typeof(ReplayInSubFunction));
        await test.ScanTypes();

        var logs = await test.GetLogs();
        Assert.Empty(logs);

        var instance = new ReplayInSubFunction();
        instance.Method6("Test");
        instance.Method1("Test");
        instance.Method2("Test");
        instance.Method2("Back");
        instance.Method3("Test");
        instance.Method4("Test");
        instance.Method4("Back");
        instance.Method5("Test");

        logs = await test.GetLogs();
        Assert.Empty(logs);
        var pushedCalls = await test.GetPushedCalls();
        Assert.Equal(8, pushedCalls.Count);
        var instances = await test.GetInstances<ReplayInSubFunction>();
        Assert.Single(instances);
        Assert.Equal(1, instances.Count(x => x.Status == FunctionInstanceStatus.Completed));
        //Assert.Equal(1, (instances[0].StateObject as ReplayInSubFunction).Counter1);

        //var waits = await test.GetWaits();
        //Assert.Equal(1, waits.Count);
        //Assert.Equal(1, waits.Count(x => x.Status == WaitStatus.Completed));
    }

    public class ReplayInSubFunction : ResumableFunctionsContainer
    {
        public int SharedCounter { get; set; }
        [ResumableFunctionEntryPoint("ReplayInSubFunctions")]
        public async IAsyncEnumerable<Wait> Test()
        {
            yield return WaitMethod<string, string>(Method6, "M6");
            yield return WaitGroup(new[]
            {
                WaitFunction(PathOneFunction("123")),
                WaitFunction(PathTwoFunction())
            }, "Wait Two Paths");//wait two sub functions
            yield return WaitMethod<string, string>(Method5, "M5").MatchAny();
        }
        public int Counter1 { get; set; }
        public int Counter2 { get; set; }

        [SubResumableFunction("PathOneFunction")]
        public async IAsyncEnumerable<Wait> PathOneFunction(string functionInput)
        {
            var x = 0;
            yield return
                   WaitMethod<string, string>(Method1, "M1")
                   .MatchAny()
                   .AfterMatch((_, _) =>
                   {
                       SharedCounter += 10;
                       x += 15;
                       if (functionInput != "123")
                           throw new Exception("Function input must be 123");
                       functionInput = "789";
                   });

            Counter1 += 10;
        M2_Wait:
            yield return
                WaitMethod<string, string>(Method2, "M2")
                .MatchAny(Counter1 == 10)
                .MatchIf(Counter1 == 13, (input, output) => input == "Back")
                .AfterMatch((_, _) =>
                {
                    if (Counter1 == 13 && x != 30)
                        throw new Exception("closure in replay problem");
                });

            Counter1 += 3;
            x += 15;
            //if (Counter1 < 16)
            //    yield return GoBackTo<string, string>("M2", (input, output) => input == "Back");
            if (Counter1 < 16)
                goto M2_Wait;
            if (functionInput != "789")
                throw new Exception("Function input must be 789");
            await Task.Delay(100);
        }

        [SubResumableFunction("PathTwoFunction")]
        public async IAsyncEnumerable<Wait> PathTwoFunction()
        {
            var x = 100;
            yield return
                  WaitMethod<string, string>(Method3, "M3")
                  .MatchAny()
                  .AfterMatch((_, _) => SharedCounter += 10);

            Counter2 += 10;
        M4:
            yield return
                WaitMethod<string, string>(Method4, "M4")
                .MatchAny(Counter2 == 10)
                .MatchIf(Counter2 == 13, (input, _) => input == "Back")
                .AfterMatch((_, _) =>
                {
                    if (!(x == 100 || x == 120))
                        throw new Exception("Locals continuation problem");
                    Console.WriteLine(x);
                });

            Counter2 += 3;
            x += 20;
            if (Counter2 < 16)
                goto M4;

            await Task.Delay(100);
        }

        [PushCall("Method1")]
        public string Method1(string input) => input + "M1";
        [PushCall("Method2")] public string Method2(string input) => input + "M2";
        [PushCall("Method3")] public string Method3(string input) => input + "M3";
        [PushCall("Method4")] public string Method4(string input) => input + "M4";
        [PushCall("Method5")] public string Method5(string input) => input + "M5";
        [PushCall("Method6")] public string Method6(string input) => input + "M6";
    }
}
