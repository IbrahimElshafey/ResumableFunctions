using ResumableFunctions.Handler;
using ResumableFunctions.Handler.Attributes;
using ResumableFunctions.Handler.BaseUse;
using ResumableFunctions.Handler.Testing;

namespace Tests
{
    public partial class ManyWaits
    {
        [Fact]
        public async Task MixedWaitsGroup_Test()
        {
            using var test = new TestShell(nameof(MixedWaitsGroup_Test), typeof(MixedWaitsGroup));
            await test.ScanTypes("MixedWaitsGroup");
            var errors = await test.GetLogs();
            Assert.Empty(errors);

            var instance = new MixedWaitsGroup();
            instance.Method1("M1");
            instance.Method2("M2");
            instance.Method3("M3");
            instance.Method4("M4");
            instance.Method5("M5");

            Assert.Empty(await test.RoundCheck(5, 8, 1));
            Assert.Equal(1, await test.GetWaitsCount(x => x.IsRoot && x.Status == ResumableFunctions.Handler.InOuts.WaitStatus.Completed));
        }
        public class MixedWaitsGroup : ResumableFunctionsContainer
        {
            [ResumableFunctionEntryPoint("MixedWaitsGroup")]
            public async IAsyncEnumerable<Wait> WaitThreeAtStart()
            {
                yield return WaitGroup(new[] {
                    WaitGroup(new Wait[] {
                        WaitMethod<string, string>(Method1, "Method 1"),
                        WaitMethod<string, string>(Method2, "Method 2"),
                        WaitMethod<string, string>(Method3, "Method 3")}
,
                    "Wait three methods in Group"                    ),
                    WaitFunction(SubFunction(), "Wait sub function"),
                    WaitMethod<string, string>(Method5, "Wait Method M5")}, "Wait Many Types Group");
                await Task.Delay(100);
                Console.WriteLine("Three method done");
            }

            [SubResumableFunction("SubFunction")]
            public async IAsyncEnumerable<Wait> SubFunction()
            {
                yield return WaitMethod<string, string>(Method4, "M4 in Sub Function").MatchAny();
            }

            [PushCall("Method1")] public string Method1(string input) => "Method1 Call";
            [PushCall("Method2")] public string Method2(string input) => "Method2 Call";
            [PushCall("Method3")] public string Method3(string input) => "Method3 Call";
            [PushCall("Method4")] public string Method4(string input) => "Method4 Call";
            [PushCall("Method5")] public string Method5(string input) => "Method5 Call";
            [PushCall("Method6")] public string Method6(string input) => "Method6 Call";
            [PushCall("Method7")] public string Method7(string input) => "Method7 Call";
        }
    }

}