﻿
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResumableFunctions.Publisher.InOuts
{
    public class MethodCall
    {
        public MethodData MethodData { get; set; }
        public string ServiceName { get; set; }
        public object Input { get; set; }
        public object Output { get; set; }
        public override string ToString()
        {
            return $"[MethodUrn:{MethodData?.MethodUrn}, \n" +
                $"Input:{JsonSerializer.Serialize(Input)}, \n" +
                $"Output:{JsonSerializer.Serialize(Output)} ]";
        }
    }


}