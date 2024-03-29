﻿using ResumableFunctions.Handler;
using ResumableFunctions.Handler.Attributes;
using ResumableFunctions.Handler.BaseUse;
using ResumableFunctions.Handler.InOuts;
using ResumableFunctions.Handler.Testing;

namespace Tests;
public class Sequence
{
    [Fact]
    public async Task SequenceFunction_Test()
    {
        using var test = new TestShell(nameof(SequenceFunction_Test), typeof(SequenceFunction));
        await test.ScanTypes();
        Assert.Empty(await test.RoundCheck(0, 0, 0));

        var function = new SequenceFunction();
        function.Method1("in1");
        Assert.Empty(await test.RoundCheck(1, 2, 0));

        function.Method2("in2");
        Assert.Empty(await test.RoundCheck(2, 3, 0));

        function.Method3("in3");
        Assert.Empty(await test.RoundCheck(3, 3, 1));
    }

    public class SequenceFunction : ResumableFunctionsContainer
    {
        [ResumableFunctionEntryPoint("ThreeMethodsSequence")]
        public async IAsyncEnumerable<Wait> ThreeMethodsSequence()
        {
            int x = 1;
            yield return WaitMethod<string, string>(Method1, "M1")
                .AfterMatch((_, _) => x++);
            //x++;
            if (x != 2)
                throw new Exception("Closure not continue");
            x++;
            yield return WaitMethod<string, string>(Method2, "M2").MatchAny();
            x++;
            if (x != 4)
                throw new Exception("Closure not continue");
            x++;
            yield return WaitMethod<string, string>(Method3, "M3").MatchAny();
            x++;
            if (x != 6)
                throw new Exception("Closure not continue");
            await Task.Delay(100);
        }

        [PushCall("RequestAdded")] public string Method1(string input) => input + "M1";
        [PushCall("Method2")] public string Method2(string input) => input + "M2";
        [PushCall("Method3")] public string Method3(string input) => input + "M3";
    }
}