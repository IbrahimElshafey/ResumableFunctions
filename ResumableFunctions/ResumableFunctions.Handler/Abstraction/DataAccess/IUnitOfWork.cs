namespace ResumableFunctions.Handler.Abstraction.Abstraction
{
    public interface IUnitOfWork : IDisposable
    {
        Task<bool> CommitAsync();
        Task Rollback();
        void MarkEntityAsModified(object entity);
    }
}
