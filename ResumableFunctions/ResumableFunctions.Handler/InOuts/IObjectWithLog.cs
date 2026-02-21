using MessagePack;
using ResumableFunctions.Handler.InOuts.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace ResumableFunctions.Handler.InOuts;

public interface IObjectWithLog
{
    [IgnoreMember]
    [NotMapped]
    public List<LogRecord> Logs { get; set; }
    
    [IgnoreMember]
    [NotMapped]
    public EntityType EntityType { get;}
}