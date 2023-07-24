﻿namespace TestSomething;

using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
internal class ExpressionCanBeConst
{
    internal void Run()
    {
        //New(typeof(Guid).GetConstructor(new[] { typeof(string) }),Constant("82c989c0-1496-4ac8-ad6c-a1df96655438"))
        Expression<Action> guid = () => new Guid("82c989c0-1496-4ac8-ad6c-a1df96655438");
        //New(typeof(DateTime).GetConstructor(new[] { typeof(long) }),Constant(638258117879255006))
        Expression<Action> date = () => new DateTime(638258117879255006);

        /*
            Call(
                typeof(JsonSerializer).GetMethod("Deserialize", 1, new[] { typeof(string), typeof(JsonSerializerOptions) }),
                Constant(<object Json>),
                MakeMemberAccess(null, typeof(JsonSerializerOptions).GetProperty("Default"))
            )
         */
        Expression<Func<ComplexClass>> complex = () => JsonSerializer.Deserialize<ComplexClass>("{Id:1234,Name:'Ibrahim'}", JsonSerializerOptions.Default);
    }

    public class ComplexClass
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

}