using ResumableFunctions.Handler;
using ResumableFunctions.Handler.Attributes;
using ResumableFunctions.Handler.Expressions;
using ResumableFunctions.Handler.Helpers;
using ResumableFunctions.Handler.InOuts;
using ResumableFunctions.Handler.InOuts.Entities;
using System.Dynamic;
using System.Reflection.Emit;

namespace TestSomething
{
    public class TestRewriteMatch : ResumableFunctionsContainer
    {
        public int InstanceId { get; set; } = 5;

        [PushCall("TestMethodOne")]
        public int TestMethodOne(string input) => input.Length;

        [PushCall("TestMethodTwo")]
        public MethodOutput TestMethodTwo(MethodInput input) => new MethodOutput { TaskId = input.Id };

        public MethodWaitEntity WaitMethodOne()
        {
            var methodWait = new MethodWaitEntity<string, int>(TestMethodOne)
                        .MatchIf((x, y) => y == InstanceId || x == (InstanceId + 10).ToString() && y <= Math.Max(10, 100))
                        .AfterMatch((input, output) => InstanceId = output);
            methodWait.CurrentFunction = this;
            return methodWait;
        }

        private int[] IntArrayMethod() => new int[] { 12, 13, 14, 15, };
        public MethodWaitEntity WaitMethodTwo()
        {
            var localVariable = GetString();
            var methodWait = new MethodWaitEntity<MethodInput, MethodOutput>(TestMethodTwo)
                       .MatchIf((x, y) =>
                       //!(y.TaskId == InstanceId + 10 &&
                       //x.Id > 12) &&
                       x.Id == InstanceId + 20 &&
                       //y.DateProp == DateTime.Today &&
                       //y.ByteArray == new byte[] { 12, 13, 14, 15, } ||
                       //y.IntArray[0] == IntArrayMethod()[2] ||
                       //y.IntArray == IntArrayMethod() &&
                       //11 + 1 == 12 &&
                       //y.GuidProp == new Guid("ab62534b-2229-4f42-8f4e-c287c82ec760") &&
                       //y.EnumProp == (StackBehaviour.Pop1 | StackBehaviour.Pop1_pop1) ||
                       y.EnumProp == StackBehaviour.Popi_popi_popi &&
                       x.IsMan &&
                       x.Name == "Mohamed"
                       )
                       .AfterMatch((input, output) => InstanceId = output.TaskId);
            methodWait.CurrentFunction = this;
            return methodWait;
        }

        private object GetString()
        {
            return "kjlklk";
        }

        public void Run()
        {
            TestWithComplexTypes();
            //TestWithBasicTypes();
        }

        class PointXY
        {
            public int X { get; set; }
            public int Y { get; set; }
        }

        private void TestWithComplexTypes()
        {
            var pushedCall = new InputOutput
            {
                Input = new MethodInput
                {
                    Id = 25,//25
                    Name = "Mohamed",//"Mohamed"
                    IsMan = true//true
                },
                Output = new MethodOutput
                {
                    TaskId = 20,
                    GuidProp = new Guid("7ec03d6d-64e3-4240-bbdc-a143e327a3fc"),
                    DateProp = new DateTime(1999, 12, 2),
                    ByteArray = new byte[] { 22, 34, 45 },
                    IntArray = new int[] { 22, 34, 45 },
                    EnumProp = StackBehaviour.Popi_popi_popi,//StackBehaviour.Popi_popi_popi
                }
            };
            var instanceDynamic = this.ToExpando();
            var dynamicPushedCall = pushedCall.ToExpando();
            //input.Id == InstanceId + 20 && output.EnumProp == StackBehaviour.Popi_popi_popi && input.IsMan && input.Name == localVariable
            var wait = WaitMethodTwo();
            //var matchRewriter = new MatchExpressionWriter(wait.MatchExpression, this);
            //Expression<Func<ExpandoObject, ExpandoObject, bool>> matchDynamic = (inputOutput, instance) =>
            //inputOutput.Get<int>("input.Id") == instance.Get<int>("InstanceId") + 20 && (bool)instance.Get("uuu");
            //var matchDynamic = matchRewriter.MatchExpressionDynamic;
            //var matchDynComp = matchDynamic.CompileFast();
            //var resu = matchDynComp.Invoke(dynamicPushedCall, instanceDynamic);


            //MandatoryPartExpression(matchRewriter, dynamicPushedCall, pushedCall);
        }

        private void MandatoryPartExpression(MatchExpressionWriter matchRewriter, ExpandoObject dynamicPushedCall, InputOutput pushedCall)
        {
            //var callMandatoryPartExpression = matchRewriter.CallMandatoryPartPaths;
            //var compiled = callMandatoryPartExpression.CompileFast();
            //var result = compiled.DynamicInvoke(pushedCall.Input, pushedCall.Output);

            ////new[] { ((int)output.EnumProp).ToString(), input.Id.ToString(), input.IsMan.ToString() }
            ////var mandatoryDynExps = matchRewriter.CallMandatoryPartExpressionDynamic;
            ////var mandatoryDynExpsCompiled = mandatoryDynExps.CompileFast();
            ////var dynresult2 = mandatoryDynExpsCompiled.DynamicInvoke(dynamicPushedCall);

            //var instanceMandexp = matchRewriter.InstanceMandatoryPartExpression;
            //var instanceMandexpComp = instanceMandexp.CompileFast();
            //var dynresult3 = (object[])instanceMandexpComp.DynamicInvoke(this);
            //var id = string.Join("#", dynresult3);
        }

        private void TestWithBasicTypes()
        {
            //var wait1 = WaitMethodOne();
            //var matchRewrite1 = new MatchExpressionWriter(wait1.MatchExpression, this);
            //var method1 = (Func<string, int, TestRewriteMatch, bool>)matchRewrite1.MatchExpression.CompileFast();
            //var exprssionAsString1 = matchRewrite1.MatchExpression.ToString();
            //var result = method1.Invoke("12345", 5, this);
            //result = method1.Invoke("123456", 6, this);


            //var pushedCall1 = JsonConvert.DeserializeObject<JObject>("""
            //    {
            //        "input":"12345",
            //        "output":5
            //    }
            //    """);
            //var pushedCall2 = JsonConvert.DeserializeObject<JObject>("""
            //    {
            //        "input":"123456",
            //        "output":6
            //    }
            //    """);
            //var jsonCompiled = (Func<JObject, bool>)matchRewrite1.MatchExpressionWithJson.CompileFast();
            //result = jsonCompiled.Invoke(pushedCall1);
            //result = jsonCompiled.Invoke(pushedCall2);
        }
    }
}
