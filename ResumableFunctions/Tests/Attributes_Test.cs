using ResumableFunctions.Handler;
using ResumableFunctions.Handler.Attributes;
using ResumableFunctions.Handler.BaseUse;
using ResumableFunctions.Handler.Testing;

namespace Tests;

public class Attributes_Test
{
    [Fact]
    public async Task NotSyncFunction_Test()
    {
        using var test = new TestShell(nameof(NotSyncFunction_Test), typeof(AttributesUsageClass));
        await test.ScanTypes();
        var errorLogs = await test.GetLogs();
        Assert.NotEmpty(errorLogs);
    }

    public class AttributesUsageClass : ResumableFunctionsContainer
    {
        [ResumableFunctionEntryPoint("NotAsync")]
        public IAsyncEnumerable<Wait> NotAsync()
        {
            throw new NotImplementedException();
        }

        [PushCall("MethodToWait")]
        public string MethodToWait(string input)
        {
            return input?.Length.ToString();
        }
    }
}