using ResumableFunctions.Handler.Abstraction.Abstraction;
using ResumableFunctions.Handler.InOuts.Entities;

namespace ResumableFunctions.Handler.DataAccess;

internal class WaitProcessingRecordsRepo : IWaitProcessingRecordsRepo
{
    private readonly WaitsDataContext _context;

    public WaitProcessingRecordsRepo(WaitsDataContext context)
    {
        _context = context;
    }

    public WaitProcessingRecord Add(WaitProcessingRecord waitProcessingRecord)
    {
        _context.WaitProcessingRecords.Add(waitProcessingRecord);
        return waitProcessingRecord;
    }
}