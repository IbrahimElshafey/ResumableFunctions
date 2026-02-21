using ResumableFunctions.Handler;
using ResumableFunctions.Handler.Attributes;
using ResumableFunctions.Handler.BaseUse;
using ResumableFunctions.Handler.Testing;

namespace Tests
{
    public class DeleteUnusedTemplateSiblingsTest
    {
        [Fact]
        public async Task DeleteUnusedTemplateSiblings_Test()
        {
            using var test = new TestShell(nameof(DeleteUnusedTemplateSiblings_Test), typeof(Test));
            await test.ScanTypes();
            var timeWaitId = await test.RoundCheck(0, 0, 0);

            var instance = new Test();
            instance.Method1("1");
            instance.Method1("1");
            instance.Method1("1");
            instance.Method1("2");
            instance.Method1("2");
            instance.Method1("2");
            instance.Method1("#");
            instance.Method1("#");
            instance.Method1("#");
            instance.Method1("#");
            instance.Method1("#");
            instance.Method1("#");
            instance.Method1("#");
            instance.Method1("#");
            instance.Method1("#");
            instance.Method1("#");
            instance.Method1("#");
            instance.Method1("#");
            instance.Method1("#");
            instance.Method1("#");
            instance.Method1("#");
            instance.Method1("#");
            instance.Method1("#");
            instance.Method1("#");
            Assert.Empty(await test.RoundCheck(24, 24, 24));
            Assert.Equal(1, await test.GetTemplatesCount());
        }

        public class Test : ResumableFunctionsContainer
        {
            [ResumableFunctionEntryPoint("DeleteUnusedTemplateSiblingsTest")]
            public async IAsyncEnumerable<Wait> DeleteUnusedTemplateSiblingsTest()
            {
                var x = 10;
                var _dynamicProp = Random.Shared.Next(2, 100);
                yield return
                    WaitMethod<string, string>(Method1, "M1")
                    .MatchIf((input, output) => _dynamicProp > 1 && x == 10);
                //.MatchIf((input, output) => _dynamicProp > 1);
                await Task.Delay(100);

                Console.WriteLine(x);
            }

            [PushCall("Method1")] public string Method1(string input) => input + "M1";
        }
    }
}