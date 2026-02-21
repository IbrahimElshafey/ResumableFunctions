using ResumableFunctions.Handler.InOuts;

namespace ResumableFunctions.Handler.Core.Abstraction
{
    public interface IExternalCallReceiver
    {
        Task<int> ReceiveExternalCall(ExternalCallArgs externalCall);
    }
}