using ResumableFunctions.Handler;
using ResumableFunctions.Handler.Attributes;
using ResumableFunctions.Handler.BaseUse;
using ResumableFunctions.Handler.InOuts;
using ResumableFunctions.Handler.Testing;

namespace Tests
{

    public partial class ManyWaits
    {

        [Fact]
        public async Task WaitTwoMethodsAfterFirst_Test()
        {
            using var test = new TestShell(nameof(WaitTwoMethodsAfterFirst_Test), typeof(WaitTwoMethodsAfterFirst));
            await test.ScanTypes();
            var errors = await test.GetLogs();
            Assert.Empty(errors);

            var wms = new WaitTwoMethodsAfterFirst();
            wms.Method4("1");
            wms.Method5("2");
            wms.Method6("3");

            var pushedCalls = await test.GetPushedCalls();
            Assert.Equal(3, pushedCalls.Count);
            errors = await test.GetLogs();
            Assert.Empty(errors);
            var waits = await test.GetWaits();
            Assert.Equal(4, waits.Count);
            Assert.Equal(4, waits.Where(x => x.Status == WaitStatus.Completed).Count());
            Assert.Equal(2, waits.Where(x => x.IsRoot).Count());

            wms = new WaitTwoMethodsAfterFirst();
            wms.Method4("1");
            wms.Method5("2");
            wms.Method6("3");

            pushedCalls = await test.GetPushedCalls();
            Assert.Equal(6, pushedCalls.Count);
            errors = await test.GetLogs();
            Assert.Empty(errors);
            waits = await test.GetWaits();
            Assert.Equal(8, waits.Count);
            Assert.Equal(8, waits.Where(x => x.Status == WaitStatus.Completed).Count());
            Assert.Equal(4, waits.Where(x => x.IsRoot).Count());
        }

        public class WaitTwoMethodsAfterFirst : ResumableFunctionsContainer
        {


            [ResumableFunctionEntryPoint("TwoMethodsAfterFirst")]
            public async IAsyncEnumerable<Wait> TwoMethodsAfterFirst()
            {
                yield return WaitMethod<string, string>(Method4, "Method 4");
                yield return WaitGroup(new[] {
                        WaitMethod<string, string>(Method5, "Method 5").MatchAny(),
                        WaitMethod<string, string>(Method6, "Method 6").MatchAny()}
,
                    "Two Methods After First");
                await Task.Delay(100);
            }

            public int Counter { get; set; }
            public int CancelCounter { get; set; }
            [PushCall("RequestAdded")]
            public string Method1(string input) => "RequestAdded Call";
            [PushCall("Method2")]
            public string Method2(string input) => "Method2 Call";
            [PushCall("Method4")] public string Method4(string input) => "Method4 Call";
            [PushCall("Method5")] public string Method5(string input) => "Method5 Call";
            [PushCall("Method6")] public string Method6(string input) => "Method6 Call";
        }

    }

}