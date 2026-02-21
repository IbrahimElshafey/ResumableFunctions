using MessagePack;
using MessagePack.Resolvers;
using System.Dynamic;

namespace ResumableFunctions.Handler.Helpers
{
    internal static class ExpandoExtensions
    {
        internal static T Get<T>(this ExpandoObject _this, string path)
        {
            var result = _this.Get(path);
            return (T)Convert.ChangeType(result, typeof(T));
        }
        internal static object Get(this ExpandoObject _this, string path)
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            var root = new Dictionary<string, object>(_this, comparer);
            var parts = path.Split('.');
            var result = root[parts[0]];
            var parent = parts.Length > 1 ? (IDictionary<object, object>)root[parts[0]] : null;
            for (var i = 1; i < parts.Length; i++)
            {
                var currentProp = parts[i];
                result = parent[currentProp];
                parent = result as IDictionary<object, object>;
            }
            return result;
        }

        internal static void Set(this ExpandoObject _this, string index, object value)
        {
            var root = (IDictionary<string, object>)_this;
            var parts = index.Split('.');
            if (parts.Length == 1)
                root[index] = value;
            else
            {
                var parent = (IDictionary<object, object>)root[parts[0]];
                for (var i = 1; i < parts.Length; i++)
                {
                    var currentProp = parts[i];
                    if (i == parts.Length - 1)
                        parent[currentProp] = value;
                    parent = parent[currentProp] as IDictionary<object, object>;
                }
            }
        }

        internal static T ToObject<T>(this ExpandoObject _this)
        {
            var blob = MessagePackSerializer.Serialize(_this, ContractlessStandardResolver.Options);
            return MessagePackSerializer.Deserialize<T>(blob, ContractlessStandardResolver.Options);
        }

        internal static object ToObject(this ExpandoObject _this, Type type)
        {
            var blob = MessagePackSerializer.Serialize(_this, ContractlessStandardResolver.Options);
            return MessagePackSerializer.Deserialize(type, blob, ContractlessStandardResolver.Options);
        }

        internal static ExpandoObject ToExpando(this object _this)
        {
            var blob = MessagePackSerializer.Serialize(_this, ContractlessStandardResolver.Options);
            return MessagePackSerializer.Deserialize<ExpandoObject>(blob, ContractlessStandardResolver.Options);
        }
    }
}