using MessagePack;
using MessagePack.Resolvers;
using System.Linq.CompilerServices.TypeSystem;

namespace ResumableFunctions.Handler.Helpers;
internal class BinarySerializer
{

    private MessagePackSerializerOptions Options() => ContractlessStandardResolver.Options;
    public byte[] ConvertToBinary(object obj)
    {
        try
        {
            // Do this once and store it for reuse.
            var resolver = CompositeResolver.Create(

                // finally use standard resolver
                StandardResolver.Instance
            );
            var options = MessagePackSerializerOptions.Standard.WithResolver(resolver);

            return MessagePackSerializer.Serialize(obj, Options());

        }
        catch (Exception ex)
        {
            throw new Exception($"Error when convert object of type [{obj?.GetType().FullName}] to binary", ex);
        }
    }

    //public object ConvertToObject(byte[] bytes)
    //{
    //    try
    //    {
    //        return MessagePackSerializer.Deserialize<ExpandoObject>(bytes, Options());
    //    }
    //    catch (Exception ex)
    //    {
    //        throw new Exception("Error when convert bytes to ExpandoObject", ex);
    //    }
    //}

    public T ConvertToObject<T>(byte[] bytes)
    {
        try
        {
            return MessagePackSerializer.Deserialize<T>(bytes, Options());
        }
        catch (Exception ex)
        {
            throw new Exception($"Error when convert bytes to [{typeof(T)}]", ex);
        }
    }

    public object ConvertToObject(byte[] bytes, Type type)
    {
        try
        {
            return MessagePackSerializer.Deserialize(type, bytes, Options());
        }
        catch (Exception ex)
        {
            throw new Exception($"Error when convert bytes to [{typeof(T)}]", ex);
        }
    }
}