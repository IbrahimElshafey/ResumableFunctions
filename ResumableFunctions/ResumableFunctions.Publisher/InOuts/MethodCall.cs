using System;
using System.Text.Json;

namespace ResumableFunctions.Publisher.InOuts
{
    public class MethodCall
    {
        public MethodCall()
        {
            Created = DateTime.UtcNow;
            Created = DateTime.UtcNow;
        }
        public MethodData MethodData { get; set; }
        
        [MessagePack.IgnoreMember]
        public string[] ToServices { get; set; }
        public int MessageId { get; set; }
        public string ServiceName { get; set; }
        public object Input { get; set; }
        public object Output { get; set; }
        public DateTime Created { get; set; }
        public override string ToString()
        {
            return $"[MethodUrn:{MethodData?.MethodUrn}, \n" +
                $"Input:{JsonSerializer.Serialize(Input)}, \n" +
                $"Output:{JsonSerializer.Serialize(Output)} ]";
        }
    }


}