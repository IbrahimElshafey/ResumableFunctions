using ResumableFunctions.Handler.InOuts.Entities;

namespace ResumableFunctions.Handler.Abstraction.Abstraction;

public interface IWaitProcessingRecordsRepo
{
    WaitProcessingRecord Add(WaitProcessingRecord waitProcessingRecord);
}