using ResumableFunctions.Handler;
using ResumableFunctions.Handler.Attributes;
using ResumableFunctions.Handler.InOuts.Entities;
using System.Linq.Expressions;
using System.Reflection.Emit;

namespace Tests.Internal
{
    public class MatchRewriteTests
    {

        [Fact]
        void ClosureTest1()
        {
            int x = 10;
            Action action1 = () => x += 10;
            Action action2 = () => x += 20;

            action1();
            action2();
            Assert.Equal(40, x);
        }

        [Fact]
        public void One()
        {

            //(x, y) => x.Id == InstanceId + 20
            var wait = new Test() { InstanceId = 10 }.PublicPropsUseInMatch(MatchExpressionType.One);
            var parts = wait.MatchExpressionParts;
            Assert.NotNull(parts.InstanceMandatoryPartExpression);
            Assert.NotNull(parts.CallMandatoryPartPaths);
            Assert.Null(parts.Closure);
            Assert.True(parts.IsMandatoryPartFullMatch);
            Assert.Equal(
               parts.GetPushedCallMandatoryPart(new MethodInput { Id = 30 }, null),
               parts.GetInstanceMandatoryPart(wait.CurrentFunction));
            Assert.NotEqual(
               parts.GetPushedCallMandatoryPart(new MethodInput { Id = 40 }, null),
               parts.GetInstanceMandatoryPart(wait.CurrentFunction));
        }

        [Fact]
        public void OneClosure()
        {
            //(x, y) => x.Id == instanceId + 20
            var wait = new Test().UseClosureInMatch(MatchExpressionType.One);
            var parts = wait.MatchExpressionParts;
            Assert.NotNull(parts.InstanceMandatoryPartExpression);
            Assert.NotNull(parts.CallMandatoryPartPaths);
            Assert.NotNull(parts.Closure);
            Assert.True(parts.IsMandatoryPartFullMatch);
            Assert.Equal(
               parts.GetPushedCallMandatoryPart(new MethodInput { Id = 30 }, null),
               parts.GetInstanceMandatoryPart(wait.CurrentFunction));
            Assert.NotEqual(
               parts.GetPushedCallMandatoryPart(new MethodInput { Id = 40 }, null),
               parts.GetInstanceMandatoryPart(wait.CurrentFunction));
        }

        [Fact]
        public void Two()
        {
            //(x, y) => !(y.TaskId == InstanceId + 10 && x.Id > 12)
            var wait = new Test().PublicPropsUseInMatch(MatchExpressionType.Two);
            var parts = wait.MatchExpressionParts;
            Assert.Null(parts.InstanceMandatoryPartExpression);
            Assert.Null(parts.CallMandatoryPartPaths);
            Assert.Null(parts.Closure);
            Assert.False(parts.IsMandatoryPartFullMatch);
        }

        [Fact]
        public void TwoClosure()
        {
            //(x, y) => !(y.TaskId == instanceId + 10 && x.Id > 12)
            var wait = new Test().UseClosureInMatch(MatchExpressionType.Two);
            var parts = wait.MatchExpressionParts;
            Assert.Null(parts.InstanceMandatoryPartExpression);
            Assert.Null(parts.CallMandatoryPartPaths);
            Assert.NotNull(parts.Closure);
            Assert.False(parts.IsMandatoryPartFullMatch);
        }

        [Fact]
        public void Three()
        {
            //(x, y) => y.TaskId == InstanceId + 10 && x.Id > 12
            var wait = new Test() { InstanceId = 10 }.PublicPropsUseInMatch(MatchExpressionType.Three);
            var parts = wait.MatchExpressionParts;
            Assert.NotNull(parts.InstanceMandatoryPartExpression);
            Assert.NotNull(parts.CallMandatoryPartPaths);
            Assert.Null(parts.Closure);
            Assert.False(parts.IsMandatoryPartFullMatch);
            Assert.Equal(
                parts.GetPushedCallMandatoryPart(new MethodInput { Id = 13 }, new MethodOutput { TaskId = 20 }),
                parts.GetInstanceMandatoryPart(wait.CurrentFunction));
            Assert.NotEqual(
              parts.GetPushedCallMandatoryPart(new MethodInput { Id = 10 }, new MethodOutput { TaskId = 22 }),
              parts.GetInstanceMandatoryPart(wait.CurrentFunction));
        }

        [Fact]
        public void Four()
        {
            //(x, y) => y.DateProp == DateTime.Today
            var wait = new Test().PublicPropsUseInMatch(MatchExpressionType.Four);
            var parts = wait.MatchExpressionParts;
            Assert.NotNull(parts.InstanceMandatoryPartExpression);
            Assert.NotNull(parts.CallMandatoryPartPaths);
            Assert.Null(parts.Closure);
            Assert.True(parts.IsMandatoryPartFullMatch);
            Assert.NotNull(parts.GetInstanceMandatoryPart(wait.CurrentFunction));
            Assert.Equal(
              parts.GetPushedCallMandatoryPart(null, new MethodOutput { DateProp = DateTime.Today }),
              parts.GetInstanceMandatoryPart(wait.CurrentFunction));
            Assert.NotEqual(
              parts.GetPushedCallMandatoryPart(null, new MethodOutput { DateProp = new DateTime(2023, 12, 12) }),
              parts.GetInstanceMandatoryPart(wait.CurrentFunction));
        }

        [Fact]
        public void Five()
        {
            //(x, y) => y.ByteArray == new byte[] { 12, 13, 14, 15, }
            var wait = new Test().PublicPropsUseInMatch(MatchExpressionType.Five);
            var parts = wait.MatchExpressionParts;
            Assert.Null(parts.InstanceMandatoryPartExpression);
            Assert.Null(parts.CallMandatoryPartPaths);
            Assert.Null(parts.Closure);
            Assert.False(parts.IsMandatoryPartFullMatch);
        }

        [Fact]
        public void Six()
        {
            //(x, y) => y.EnumProp == StackBehaviour.Popi_popi_popi && x.IsMan
            var wait = new Test().PublicPropsUseInMatch(MatchExpressionType.Six);
            var parts = wait.MatchExpressionParts;
            Assert.NotNull(parts.InstanceMandatoryPartExpression);
            Assert.NotNull(parts.CallMandatoryPartPaths);
            Assert.Null(parts.Closure);
            Assert.True(parts.IsMandatoryPartFullMatch);
            Assert.Equal(
             parts.GetPushedCallMandatoryPart(new MethodInput { IsMan = true }, new MethodOutput { EnumProp = StackBehaviour.Popi_popi_popi }),
             parts.GetInstanceMandatoryPart(wait.CurrentFunction));
            Assert.NotEqual(
              parts.GetPushedCallMandatoryPart(new MethodInput { IsMan = false }, new MethodOutput { EnumProp = StackBehaviour.Popi_popi_popi }),
              parts.GetInstanceMandatoryPart(wait.CurrentFunction));
        }

        [Fact]
        public void Eight()
        {
            //(x, y) => y.EnumProp == StackBehaviour.Popi_popi_popi && !x.IsMan
            var wait = new Test().PublicPropsUseInMatch(MatchExpressionType.Eight);
            var parts = wait.MatchExpressionParts;
            Assert.NotNull(parts.InstanceMandatoryPartExpression);
            Assert.NotNull(parts.CallMandatoryPartPaths);
            Assert.Null(parts.Closure);
            Assert.True(parts.IsMandatoryPartFullMatch);
            Assert.Equal(
             parts.GetPushedCallMandatoryPart(new MethodInput { IsMan = false }, new MethodOutput { EnumProp = StackBehaviour.Popi_popi_popi }),
             parts.GetInstanceMandatoryPart(wait.CurrentFunction));
            Assert.NotEqual(
              parts.GetPushedCallMandatoryPart(new MethodInput { IsMan = true }, new MethodOutput { EnumProp = StackBehaviour.Popi_popi_popi }),
              parts.GetInstanceMandatoryPart(wait.CurrentFunction));
        }


        [Fact]
        public void Nine()
        {
            //(x, y) => y.GuidProp == new Guid("ab62534b-2229-4f42-8f4e-c287c82ec760")
            var wait = new Test().PublicPropsUseInMatch(MatchExpressionType.Nine);
            var parts = wait.MatchExpressionParts;
            Assert.NotNull(parts.InstanceMandatoryPartExpression);
            Assert.NotNull(parts.CallMandatoryPartPaths);
            Assert.Null(parts.Closure);
            Assert.True(parts.IsMandatoryPartFullMatch);
            Assert.Equal(
             parts.GetPushedCallMandatoryPart(null, new MethodOutput { GuidProp = new Guid("ab62534b-2229-4f42-8f4e-c287c82ec760") }),
             parts.GetInstanceMandatoryPart(wait.CurrentFunction));
            Assert.NotEqual(
             parts.GetPushedCallMandatoryPart(null, new MethodOutput { GuidProp = new Guid("4462534b-2229-4f42-8f4e-c287c82ec760") }),
             parts.GetInstanceMandatoryPart(wait.CurrentFunction));
        }

        [Fact]
        public void Ten()
        {
            //(x, y) => x.IsMan | IsChild && x.Id == InstanceId;
            var wait = new Test() { InstanceId = 10 }.PublicPropsUseInMatch(MatchExpressionType.Ten);
            var parts = wait.MatchExpressionParts;
            Assert.NotNull(parts.InstanceMandatoryPartExpression);
            Assert.NotNull(parts.CallMandatoryPartPaths);
            Assert.Null(parts.Closure);
            Assert.False(parts.IsMandatoryPartFullMatch);
            Assert.Equal(
             parts.GetPushedCallMandatoryPart(new MethodInput { Id = 10, IsMan = true }, null),
             parts.GetInstanceMandatoryPart(wait.CurrentFunction));
            Assert.NotEqual(
             parts.GetPushedCallMandatoryPart(new MethodInput { Id = 20 }, null),
             parts.GetInstanceMandatoryPart(wait.CurrentFunction));
        }

        [Fact]
        public void Eleven()
        {
            //(x, y) => x.Id + InstanceId == InstanceId
            var wait = new Test() { InstanceId = 10 }.PublicPropsUseInMatch(MatchExpressionType.Eleven);
            var parts = wait.MatchExpressionParts;
            Assert.Null(parts.InstanceMandatoryPartExpression);
            Assert.Null(parts.CallMandatoryPartPaths);
            Assert.Null(parts.Closure);
            Assert.False(parts.IsMandatoryPartFullMatch);
        }

        [Fact]
        public void Twelve()
        {
            //(x, y) => InstanceMethod(x.Id) == InstanceId;
            var wait = new Test() { InstanceId = 10 }.PublicPropsUseInMatch(MatchExpressionType.Twelve);
            var parts = wait.MatchExpressionParts;
            Assert.Null(parts.InstanceMandatoryPartExpression);
            Assert.Null(parts.CallMandatoryPartPaths);
            Assert.Null(parts.Closure);
            Assert.False(parts.IsMandatoryPartFullMatch);
        }

        [Fact]
        public void Thirteen()
        {
            //(x, y) => Math.Min(x.Id, 100) == InstanceId;
            var wait = new Test() { InstanceId = 10 }.PublicPropsUseInMatch(MatchExpressionType.Thirteen);
            var parts = wait.MatchExpressionParts;
            Assert.NotNull(parts.InstanceMandatoryPartExpression);
            Assert.NotNull(parts.CallMandatoryPartPaths);
            Assert.Null(parts.Closure);
            Assert.True(parts.IsMandatoryPartFullMatch);
        }

        [Fact]
        public void Fourteen()
        {
            //(x, y) => !x.IsMan
            var wait = new Test().PublicPropsUseInMatch(MatchExpressionType.Fourteen);
            var parts = wait.MatchExpressionParts;
            Assert.Null(parts.InstanceMandatoryPartExpression);
            Assert.Null(parts.CallMandatoryPartPaths);
            Assert.Null(parts.Closure);
            Assert.False(parts.IsMandatoryPartFullMatch);
        }

        [Fact]
        public void Fifteen()
        {
            //(x, y) => 11 + 1 == 12
            var wait = new Test().PublicPropsUseInMatch(MatchExpressionType.Fifteen);
            var parts = wait.MatchExpressionParts;
            Assert.Null(parts.InstanceMandatoryPartExpression);
            Assert.Null(parts.CallMandatoryPartPaths);
            Assert.Null(parts.Closure);
            Assert.False(parts.IsMandatoryPartFullMatch);
        }

        [Fact]
        public void Sixteen()
        {
            /*(x, y) =>
                (!(y.TaskId == InstanceId + 10 && x.Id > 12) &&
                x.Id == InstanceId + 20 &&
                y.DateProp == DateTime.Today &&
                y.ByteArray == new byte[] { 12, 13, 14, 15, } ||
                y.IntArray[0] == IntArrayMethod()[2] ||
                y.IntArray == IntArrayMethod()) &&
                11 + 1 == 12 &&
                y.GuidProp == new Guid("ab62534b-2229-4f42-8f4e-c287c82ec760") &&
                y.EnumProp == (StackBehaviour.Pop1 | StackBehaviour.Pop1_pop1) &&
                true | false && x.Id == InstanceId &&
                y.EnumProp == StackBehaviour.Popi_popi_popi && x.IsMan &&
                x.Name == "Mohamed"
            */
            var wait = new Test().PublicPropsUseInMatch(MatchExpressionType.Sixteen);
            var parts = wait.MatchExpressionParts;
            Assert.NotNull(parts.InstanceMandatoryPartExpression);
            Assert.NotNull(parts.CallMandatoryPartPaths);
            Assert.Null(parts.Closure);
            Assert.True(parts.IsMandatoryPartFullMatch);
        }

        public class Test : ResumableFunctionsContainer
        {
            public int InstanceId { get; set; } = 5;
            public bool IsChild { get; set; }

            //[PushCall("TestMethodOne")]
            //private int TestMethodOne(string input) => input.Length;

            [PushCall("TestMethodTwo")]
            private MethodOutput TestMethodTwo(MethodInput input) => new MethodOutput { TaskId = input.Id };

            public MethodWaitEntity UseClosureInMatch(MatchExpressionType matchExpressionType)
            {
                int instanceId = 10;
                Expression<Func<MethodInput, MethodOutput, bool>> matchExpression = matchExpressionType
                switch
                {
                    MatchExpressionType.One => (x, y) => x.Id == instanceId + 20,
                    MatchExpressionType.Two => (x, y) => !(y.TaskId == instanceId + 10 && x.Id > 12),
                    MatchExpressionType.Three => throw new NotImplementedException(),
                    MatchExpressionType.Four => throw new NotImplementedException(),
                    MatchExpressionType.Five => throw new NotImplementedException(),
                    MatchExpressionType.Six => throw new NotImplementedException(),
                    MatchExpressionType.Eight => throw new NotImplementedException(),
                    MatchExpressionType.Nine => throw new NotImplementedException(),
                    MatchExpressionType.Ten => throw new NotImplementedException(),
                    MatchExpressionType.Eleven => throw new NotImplementedException(),
                    MatchExpressionType.Twelve => throw new NotImplementedException(),
                    MatchExpressionType.Thirteen => throw new NotImplementedException(),
                    MatchExpressionType.Fourteen => throw new NotImplementedException(),
                    MatchExpressionType.Fifteen => throw new NotImplementedException(),
                    MatchExpressionType.Sixteen => throw new NotImplementedException()
                };
                return new MethodWaitEntity<MethodInput, MethodOutput>(TestMethodTwo)
                {
                    CurrentFunction = this,
                    IsRoot = true,
                }
                 .MatchIf(matchExpression)
                 .AfterMatch((input, output) => InstanceId = output.TaskId);
            }

            private int[] IntArrayMethod() => new int[] { 12, 13, 14, 15, };
            public MethodWaitEntity PublicPropsUseInMatch(MatchExpressionType matchExpressionType)
            {
                return new MethodWaitEntity<MethodInput, MethodOutput>(TestMethodTwo)
                {
                    CurrentFunction = this,
                    IsRoot = true,
                }
                .MatchIf(GetMatchExprssion(matchExpressionType))
                .AfterMatch((input, output) => InstanceId = output.TaskId);
            }

            private Expression<Func<MethodInput, MethodOutput, bool>> GetMatchExprssion(MatchExpressionType matchExpressionType)
            {
                switch (matchExpressionType)
                {
                    case MatchExpressionType.One:
                        return (x, y) => x.Id == InstanceId + 20;
                    case MatchExpressionType.Two:
                        return (x, y) => !(y.TaskId == InstanceId + 10 && x.Id > 12);
                    case MatchExpressionType.Three:
                        return (x, y) => y.TaskId == InstanceId + 10 && x.Id > 12;
                    case MatchExpressionType.Four:
                        return (x, y) => y.DateProp == DateTime.Today;
                    case MatchExpressionType.Five:
                        return (x, y) => y.ByteArray == new byte[] { 12, 13, 14, 15, };
                    case MatchExpressionType.Six:
                        return (x, y) => y.EnumProp == StackBehaviour.Popi_popi_popi && x.IsMan;
                    case MatchExpressionType.Eight:
                        return (x, y) => y.EnumProp == StackBehaviour.Popi_popi_popi && !x.IsMan;
                    case MatchExpressionType.Nine:
                        return (x, y) => y.GuidProp == new Guid("ab62534b-2229-4f42-8f4e-c287c82ec760");
                    case MatchExpressionType.Ten:
                        return (x, y) => x.IsMan | IsChild && x.Id == InstanceId;
                    case MatchExpressionType.Eleven:
                        return (x, y) => x.Id + InstanceId == InstanceId;
                    case MatchExpressionType.Twelve:
                        return (x, y) => InstanceMethod(x.Id) == InstanceId;
                    case MatchExpressionType.Thirteen:
                        return (x, y) => Math.Min(x.Id, 100) == InstanceId;
                    case MatchExpressionType.Fourteen:
                        return (x, y) => !x.IsMan;
                    case MatchExpressionType.Fifteen:
                        return (x, y) => 11 + 1 == 12;
                    case MatchExpressionType.Sixteen:
                        return (x, y) =>
                           (!(y.TaskId == InstanceId + 10 && x.Id > 12) &&
                           x.Id == InstanceId + 20 &&
                           y.DateProp == DateTime.Today &&
                           y.ByteArray == new byte[] { 12, 13, 14, 15, } ||
                           y.IntArray[0] == IntArrayMethod()[2] ||
                           y.IntArray == IntArrayMethod()) &&
                           11 + 1 == 12 &&
                           y.GuidProp == new Guid("ab62534b-2229-4f42-8f4e-c287c82ec760") &&
                           y.EnumProp == (StackBehaviour.Pop1 | StackBehaviour.Pop1_pop1) &&
                           true | false && x.Id == InstanceId &&
                           y.EnumProp == StackBehaviour.Popi_popi_popi && x.IsMan &&
                           x.Name == "Mohamed";
                    default:
                        throw new Exception("No Expression");
                }


            }

            private int InstanceMethod(int id) => id;

            private object GetString()
            {
                return "kjlklk";
            }

        }

    }

    public enum MatchExpressionType
    {
        One,
        Two,
        Three,
        Four,
        Five,
        Six,
        Eight,
        Nine,
        Ten,
        Eleven,
        Twelve,
        Thirteen,
        Fourteen,
        Fifteen,
        Sixteen,
    }
    public class PointXY
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    public class MethodOutput
    {
        public int TaskId { get; set; }
        public Guid GuidProp { get; set; }
        public DateTime DateProp { get; set; }

        public byte[] ByteArray { get; set; }
        public int[] IntArray { get; set; }
        public StackBehaviour EnumProp { get; set; }
    }
    public class MethodInput
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool IsMan { get; set; }
    }
}