using ResumableFunctions.Handler;
using ResumableFunctions.Handler.Attributes;
using ResumableFunctions.Handler.BaseUse;
using ResumableFunctions.Handler.Testing;

namespace Tests
{
    public class ExcptionInResumableFunction_Test
    {
        [Fact]
        public async Task ExceptionAtStart_Test()
        {
            using var test = new TestShell(nameof(ExceptionAtStart_Test), typeof(ExceptionAtStartTest));
            await test.ScanTypes();
            var errorLogs = await test.GetLogs();
            Assert.True(errorLogs.Count > 0);
        }

        [Fact]
        public async Task ExceptionAfterFirstWait_Test()
        {
            using var test = new TestShell(nameof(ExceptionAfterFirstWait_Test), typeof(ExceptionAfterFirstWaitTest));
            await test.ScanTypes();
            var errorLogs = await test.GetLogs();
            Assert.Empty(errorLogs);
            await test.SimulateMethodCall<ExceptionAfterFirstWaitTest>(x => x.MethodToWait("Ibrahim"), "3");
            errorLogs = await test.GetLogs();
            Assert.NotEmpty(errorLogs);
        }

    }

    public class ExceptionAfterFirstWaitTest : ResumableFunctionsContainer
    {
        public string? MethodOutput { get; set; }

        [ResumableFunctionEntryPoint("Test")]
        public async IAsyncEnumerable<Wait> Test()
        {
            yield return WaitMethod<string, string>(MethodToWait, "Wait Method")
                .MatchIf((input, output) => input.Length > 3)
                .AfterMatch((input, output) => MethodOutput = output);
            await Task.Delay(100);
            throw new Exception("Can't get any wait");
        }

        [PushCall("MethodToWait")]
        public string MethodToWait(string input)
        {
            return input?.Length.ToString();
        }
    }
    public class ExceptionAtStartTest : ResumableFunctionsContainer
    {


        [ResumableFunctionEntryPoint("ExceptionAtStartTest")]
        public async IAsyncEnumerable<Wait> ExceptionAtStart()
        {
            throw new Exception("Can't get any wait");
            yield return WaitMethod<string, string>(MethodToWait, "Wait Method")
                .MatchIf((input, output) => input.Length > 3);
            await Task.Delay(100);
        }

        [PushCall("MethodToWait")]
        public string? MethodToWait(string input)
        {
            return input?.Length.ToString();
        }
    }
}