using FastExpressionCompiler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ResumableFunctions.Handler.Helpers;
using ResumableFunctions.Handler.InOuts.Entities.EntityBehaviour;
using System.ComponentModel.DataAnnotations.Schema;
using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;

namespace ResumableFunctions.Handler.InOuts.Entities;

public class PushedCall : IEntity<long>, IBeforeSaveEntity
{
    public long Id { get; internal set; }
    /// <summary>
    /// MethodInfo of the method that was called and also some RF attribtes like
    ///  CanPublishFromExternal, IsLocalOnly, IsActive
    /// </summary>
    [NotMapped]
    public MethodData MethodData { get; internal set; }
    /// <summary>
    /// Same as MethodData but in binary format
    /// </summary>
    //Todo: Replace with MethodDataId because it's large and is same for all calls form the same method
    public byte[] MethodDataValue { get; internal set; }
    [NotMapped]
    public InputOutput Data { get; internal set; } = new();
    public byte[] DataValue { get; internal set; }
    public int? ServiceId { get; internal set; }

    public DateTime Created { get; internal set; }
    public string MethodUrn { get; internal set; }

    internal string GetMandatoryPart(List<string> callMandatoryPartPaths)
    {
        if (callMandatoryPartPaths?.Any() != true) return null;

        var converter = new BinarySerializer();
        var raw = converter.ConvertToObject<ExpandoObject>(DataValue);

        var values = callMandatoryPartPaths.Select(path =>
        {
            return raw.Get(path)?.ToString();
        });

        return JsonConvert.SerializeObject(values);
    }

    public void BeforeSave()
    {
        var converter = new BinarySerializer();
        DataValue = converter.ConvertToBinary(Data);
        MethodDataValue = converter.ConvertToBinary(MethodData);
        MethodUrn = MethodData?.MethodUrn;
    }

    public void LoadUnmappedProps(MethodInfo methodInfo = null)
    {
        var converter = new BinarySerializer();
        if (methodInfo == null)
            Data = converter.ConvertToObject<InputOutput>(DataValue);
        else
        {
            var inputType = methodInfo.GetParameters()[0].ParameterType;
            var outputType = methodInfo.IsAsyncMethod() ?
                methodInfo.ReturnType.GetGenericArguments()[0] :
                methodInfo.ReturnType;
            Data = GetMethodData(inputType, outputType, DataValue);
        }
        MethodData = converter.ConvertToObject<MethodData>(MethodDataValue);
    }

    private static InputOutput GetMethodData(Type inputType, Type outputType, byte[] dataBytes)
    {
        var converter = new BinarySerializer();
        var genericInputOutPut = typeof(GInputOutput<,>).MakeGenericType(inputType, outputType);
        dynamic data = converter.ConvertToObject(dataBytes, genericInputOutPut);
        return InputOutput.FromGeneric(data);
    }

    public override string ToString()
    {
        return JsonConvert.SerializeObject(
            this,
            Formatting.None,
            new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore
            });
    }
}