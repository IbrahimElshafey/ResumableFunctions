﻿using Microsoft.CodeAnalysis;
using System;

namespace ResumableFunctions.CodeGeneration
{
    [Generator]
    public class HelloSourceGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            // Find the main method

            // Build up the source code
            string source = $@"// <auto-generated/>
using System;

namespace Test
{{
    public static class TestClass
    {{
        static  void HelloFrom(string name) =>
            Console.WriteLine($""Generator says: Hi from '{{name}}'"");
    }}
}}
";

            // Add the source code to the compilation
            context.AddSource($"TestClass.g.cs", source);
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            // No initialization required for this one
        }
    }
}