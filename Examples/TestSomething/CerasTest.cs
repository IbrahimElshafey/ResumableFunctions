﻿using Ceras;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TestSomething
{
    class Person
    {
        public Person(int x)
        {

        }
        public string Name;
        public int Age;
    }
    internal class CerasTest
    {
        public void Run()
        {
            var p = new Person(1) { Name = "riki", Age = 5 };
            var o = RuntimeHelpers.GetUninitializedObject(typeof(Person));
            var ceras = new CerasSerializer();
            var bytes = ceras.Serialize<dynamic>(p);
            var p2 = ceras.Deserialize<Person>(bytes);
            Expression exp = () => 10 + 12;
            bytes = ceras.Serialize(exp);
            var exp2 = ceras.Deserialize<LambdaExpression>(bytes);
        }
    }
}
