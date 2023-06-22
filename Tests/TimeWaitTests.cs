using ClientOnboarding.InOuts;
using ClientOnboarding.Services;
using ClientOnboarding.Workflow;
using Microsoft.Extensions.DependencyInjection;
using ResumableFunctions.Handler;
using ResumableFunctions.Handler.Attributes;
using ResumableFunctions.Handler.Helpers;
using ResumableFunctions.Handler.InOuts;
using ResumableFunctions.Handler.TestShell;

namespace Tests
{
    public partial class TimeWaitTests
    {
        [Fact]
        public async Task TestTimeWaitAtStrat_Test()
        {
            var test = new TestCase(nameof(TestTimeWaitAtStrat_Test), typeof(TimeWaitWorkflow));
            await test.ScanTypes();
            var timeWaitId = await RoundTest(test, 1);

            await test.SimulateMethodCall<LocalRegisteredMethods>(
                x => x.TimeWait(new TimeWaitInput { TimeMatchId = timeWaitId }), true);
            timeWaitId = await RoundTest(test, 2);

            await test.SimulateMethodCall<LocalRegisteredMethods>(
                x => x.TimeWait(new TimeWaitInput { TimeMatchId = timeWaitId }), true);
            await RoundTest(test, 3);
        }

        private async Task<string> RoundTest(TestCase test, int round)
        {
            var pushedCalls = await test.GetPushedCalls();
            var waits = await test.GetWaits(null, true);
            var instances = await test.GetInstances<TimeWaitWorkflow>(true);
            Assert.Equal(round - 1, pushedCalls.Count);
            Assert.Equal(round, waits.Count);
            Assert.Equal(round, instances.Count);
            var errors = await test.GetErrors();
            Assert.Empty(errors);
            return (waits.First(x => x.IsFirst) as MethodWait).MandatoryPart;
        }
    }

    public class TimeWaitWorkflow : ResumableFunction
    {
        public string TimeWaitId { get; set; }

        [ResumableFunctionEntryPoint("TestTimeWait")]
        public async IAsyncEnumerable<Wait> TestTimeWaitAtStrat()
        {
            yield return
                Wait(TimeSpan.FromDays(2))
                .SetData(x => TimeWaitId == x.TimeMatchId);
            Console.WriteLine("Time wait at start matched.");
        }
    }
}