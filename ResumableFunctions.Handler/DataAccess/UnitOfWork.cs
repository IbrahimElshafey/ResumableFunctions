using Microsoft.EntityFrameworkCore;
using ResumableFunctions.Handler.Abstraction.Abstraction;

namespace ResumableFunctions.Handler.DataAccess;

internal class UnitOfWork : IUnitOfWork
{
    private readonly WaitsDataContext _context;

    public UnitOfWork(WaitsDataContext context) =>
        _context = context;

    public async Task<bool> CommitAsync()
    {
        

        // Possibility to dispatch domain events, etc

        return await _context.SaveChangesAsync() > 0;
    }

    public void Dispose() => _context.Dispose();

    public Task Rollback()
    {
        // Rollback anything, if necessary
        return Task.CompletedTask;
    }

    public void MarkEntityAsModified(object entity)
    {
        _context.Entry(entity).State = EntityState.Modified;
    }
}
