using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;

namespace ResumableFunctions.Handler.Helpers
{
    public class PrivateDataResolver : DefaultContractResolver
    {
        static PrivateDataResolver contractResolver = new PrivateDataResolver();
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
                        member.MemberType.CanConvertToSimpleString() &&
                        !member.Name.StartsWith("<>") &&//not a clsoure or compiler generated field
                        //!member.Name.EndsWith("__BackingField") &&//Backing Field
                        !member.Name.StartsWith("<GroupMatchFuncName>")//
                        )
               .Select(parameter => base.CreateProperty(parameter, memberSerialization))
               .ToList();
            props.ForEach(p => { p.Writable = true; p.Readable = true; });
            return props;
        }
    }
}
