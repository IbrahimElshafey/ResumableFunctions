﻿
namespace ResumableFunctions.Handler.InOuts;

public class ResumableFunctionState : IEntityWithUpdate,IEntityWithDelete
{
    public ResumableFunctionState()
    {

    }
    public int Id { get; internal set; }



    /// <summary>
    /// Serailized class instance that contain the resumable function class
    /// </summary>
    public object StateObject { get; internal set; }

    public List<Wait> Waits { get; internal set; } = new();
    public List<FunctionStateLogRecord> LogRecords { get; internal set; } = new();
    public ResumableFunctionIdentifier ResumableFunctionIdentifier { get; set; }
    public int ResumableFunctionIdentifierId { get; set; }
    public LogStatus Status { get; set; }
    public DateTime Modified { get; internal set; }
    public string ConcurrencyToken { get; internal set; }
    public DateTime Created { get; internal set; }

    public bool IsDeleted { get; internal set; }

    public void AddLog(LogStatus status, string statusMessage)
    {
        Status = status;
        LogRecords.Add(new FunctionStateLogRecord { Status = status, StatusMessage = statusMessage });
    }

}
