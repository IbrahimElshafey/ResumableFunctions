﻿using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;
using System.Reflection;

namespace ResumableFunctions.Handler.Helpers
{
    public class ClosureContractResolver : DefaultContractResolver
    {
        static ClosureContractResolver contractResolver = new ClosureContractResolver();
        internal static JsonSerializerSettings Settings { get; } = 
            new JsonSerializerSettings { ContractResolver = contractResolver };

        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            var props = type
               .GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
               .Where(member => member is FieldInfo && !member.Name.StartsWith("<>"))
               .Select(parameter => base.CreateProperty(parameter, memberSerialization))
               .ToList();
            props.ForEach(p => { p.Writable = true; p.Readable = true; });
            return props;
        }
    }
}