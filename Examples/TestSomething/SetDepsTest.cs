﻿using System.Reflection;
using System.Runtime.CompilerServices;
using TestSomething;

internal class SetDepsTest
{
    public void Run()
    {
        var type = typeof(MyClass);
        var instance = RuntimeHelpers.GetUninitializedObject(type);
        var setDepsMi = type.GetMethod("SetDeps", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (setDepsMi == null)
        {
            Console.WriteLine("Warn: No set deps method found that match the criteria.");
            return;
        }

        var parameters = setDepsMi.GetParameters();
        var inputs = new object[parameters.Count()];
        if (setDepsMi.ReturnType == typeof(void) &&
            parameters.Count() >= 1)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                var par = parameters[i];
                if (par.ParameterType == typeof(int))
                    inputs[i] = Random.Shared.Next();
                if (par.ParameterType == typeof(string))
                    inputs[i] = Random.Shared.NextDouble().ToString();
            }
        }
        else
        {
            Console.WriteLine("Warn: No set deps method found that match the criteria.");
        }
        setDepsMi.Invoke(instance, inputs);
    }
}

public class MyClass
{
    private int x;
    private string y;

    private void SetDeps(int x, string y)
    {
        this.x = x;
        this.y = y;
    }
}