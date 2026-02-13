using ResumableFunctions.Handler.InOuts.Entities;

namespace ResumableFunctions.Handler.Abstraction.Abstraction
{
    public interface IPrivateDataRepo
    {
        Task<PrivateData> GetPrivateData(long id);
    }
}