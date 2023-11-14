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
        public async Task WaitManyMethodsWithExpression_Test()
        {
            using var test = new TestShell(nameof(WaitManyMethodsWithExpression_Test), typeof(WaitManyMethodsWithExpression));
            await test.ScanTypes();
            var errors = await test.GetLogs();
            Assert.Empty(errors);

            var wms = new WaitManyMethodsWithExpression();
            wms.Method2("1");
            wms.Method3("1");

            var pushedCalls = await test.GetPushedCalls();
            Assert.Equal(2, pushedCalls.Count);
            errors = await test.GetLogs();
            Assert.Empty(errors);
            var waits = await test.GetWaits();
            Assert.Equal(4, waits.Count);
            Assert.Equal(3, waits.Count(x => x.Status == WaitStatus.Completed));
            Assert.Equal(1, waits.Count(x => x.Status == WaitStatus.Canceled));
            Assert.Equal(1, waits.Count(x => x.IsRoot));


            wms.Method1("1");
            wms.Method3("1");

            pushedCalls = await test.GetPushedCalls();
            Assert.Equal(4, pushedCalls.Count);
            errors = await test.GetLogs();
            Assert.Empty(errors);
            waits = await test.GetWaits();
            Assert.Equal(8, waits.Count);
            Assert.Equal(6, waits.Count(x => x.Status == WaitStatus.Completed));
            Assert.Equal(2, waits.Count(x => x.Status == WaitStatus.Canceled));
            Assert.Equal(2, waits.Count(x => x.IsRoot));
        }
        public class WaitManyMethodsWithExpression : ResumableFunctionsContainer
        {
            public int PublicCounter { get; set; } = 10;
            [ResumableFunctionEntryPoint("WaitManyWithExpression")]
            public async IAsyncEnumerable<Wait> WaitManyWithExpression()
            {
                int localCounter = 10;
                yield return Wait(new[]
                    {
                        Wait<string, string>(Method1, "Method 1")
                            .MatchIf((_,_) => localCounter == 10),
                        Wait<string, string>(Method2, "Method 2"),
                        Wait<string, string>(Method3, "Method 3")
                    }
,
                    "Wait three methods")
                //.MatchIf(group => group.CompletedCount == 2 && Id == 10 && x == 1);
                .MatchIf(group =>
                {
                    if (localCounter % 10 != 0)
                        throw new Exception("Closure in group match filter not work");
                    localCounter += 10;
                    PublicCounter += 10;
                    return group.CompletedCount == 2;
                });
                //.MatchIf(group => group.CompletedCount == 2);
                await Task.Delay(100);
                if (localCounter != 30)
                    throw new Exception("Closure in group match filter not UPDATED.");
                if (PublicCounter != 20)
                    throw new Exception("Updating Counter in group match filter faileds.");
                Console.WriteLine("Three method done");
            }

            [PushCall("RequestAdded")]
            public string Method1(string input) => "RequestAdded Call";
            [PushCall("Method2")]
            public string Method2(string input) => "Method2 Call";
            [PushCall("Method3")] public string Method3(string input) => "Method3 Call";
        }

    }

}