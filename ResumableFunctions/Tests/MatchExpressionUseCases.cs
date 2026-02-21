using ResumableFunctions.Handler;
using ResumableFunctions.Handler.Attributes;
using ResumableFunctions.Handler.BaseUse;
using ResumableFunctions.Handler.InOuts;
using ResumableFunctions.Handler.Testing;

namespace Tests;

public class MatchExpressionUseCases
{
    [Fact]
    public async Task MatchExpressionUseCases_Test()
    {
        using var test = new TestShell(nameof(MatchExpressionUseCases_Test), typeof(TestClass));
        await test.ScanTypes();

        var logs = await test.GetLogs();
        Assert.Empty(logs);

        var instance = new TestClass();
        instance.Method6("Test");

        logs = await test.GetLogs();
        Assert.Empty(logs);
        var pushedCalls = await test.GetPushedCalls();
        Assert.Single(pushedCalls);
        var instances = await test.GetInstances<TestClass>();
        Assert.Single(instances);
        Assert.Equal(1, instances.Count(x => x.Status == FunctionInstanceStatus.Completed));
    }

    public class TestClass : ResumableFunctionsContainer
    {
        [MessagePack.IgnoreMember]
        public Dep1 dep1;//must be public if used in the expression trees and [MessagePack.IgnoreMember] to not serialize it
        private void SetDependencies()
        {
            dep1 = new Dep1(5);
        }

        [ResumableFunctionEntryPoint("MatchWithInstanceMethodCall")]
        public async IAsyncEnumerable<Wait> Test()
        {
            yield return
                WaitMethod<string, string>(Method6, "M6")
                .MatchIf((input, output) => 
                input == "Test" && //normal expression
                InstanceCall(input, output) && //instance call in current class
                dep1.MethodIndep(input) > 0 && //instance method in dependacies
                TestClass.StaticMethod(input) //Static method in current class
                );
        }

        public static bool StaticMethod(string input)
        {
            return input.Length == 4;
        }
        private bool InstanceCall(string input, string output)
        {
            return output == "TestM6" && input.Length == 4;
        }

        [PushCall("Method6")] public string Method6(string input) => input + "M6";
    }

    public class Dep1
    {
        public Dep1(int b)
        {

        }
        public int MethodIndep(string input) => input.Length;
    }
}
