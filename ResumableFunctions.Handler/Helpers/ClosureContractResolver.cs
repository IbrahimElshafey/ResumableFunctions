﻿using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;

namespace ResumableFunctions.Handler.Helpers
{
    public class ClosureContractResolver : DefaultContractResolver
    {
        static ClosureContractResolver contractResolver = new ClosureContractResolver();
        internal static JsonSerializerSettings Settings { get; } =
            new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore,
                ContractResolver = contractResolver
            };

        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            var props = type
               .GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
               .Where(member =>
                        member is FieldInfo &&
                        !member.Name.StartsWith("<>") &&
                        !member.Name.StartsWith("<GroupMatchFuncName>")
                        )
               .Select(parameter => base.CreateProperty(parameter, memberSerialization))
               .ToList();
            props.ForEach(p => { p.Writable = true; p.Readable = true; });
            return props;
        }
    }
}
