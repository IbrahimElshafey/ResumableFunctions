﻿using System.Reflection;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json;

namespace ResumableFunctions.Handler.Helpers;

public class TypeToStringConverter : ValueConverter<Type, string>
{
    public TypeToStringConverter()
        : base(
            type => TypeToString(type),
            text => StringToType(text))
    {
    }

    public class SystemTypeClone
    {
        public string Name { get; set; }
        public string AssemblyPath { get; set; }
    }

    private static Type StringToType(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var typeObject = JsonConvert.DeserializeObject<SystemTypeClone>(text);
        var assembly = Assembly.LoadFile(typeObject.AssemblyPath);
        //Extensions.SetCurrentFunctionAssembly(assembly);
        return assembly.GetType(typeObject.Name)!;
    }

    private static string TypeToString(Type type)
    {
        if (type == null) return null;
        var typeObject = new SystemTypeClone { Name = type.Name, AssemblyPath = type.Assembly.Location };
        return JsonConvert.SerializeObject(typeObject, Formatting.Indented);
    }
}