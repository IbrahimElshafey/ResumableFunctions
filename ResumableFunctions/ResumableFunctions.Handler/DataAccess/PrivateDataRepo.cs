using ResumableFunctions.Handler.Abstraction.Abstraction;
using ResumableFunctions.Handler.InOuts.Entities;

namespace ResumableFunctions.Handler.DataAccess;

internal class PrivateDataRepo : IPrivateDataRepo
{
    private readonly WaitsDataContext _context;

    public PrivateDataRepo(WaitsDataContext context)
    {
        _context = context;
    }

    public async Task<PrivateData> GetPrivateData(long id)
    {
        return await _context.PrivateData.FindAsync(id);
    }
}
