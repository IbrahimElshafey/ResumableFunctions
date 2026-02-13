using ResumableFunctions.Handler.InOuts;

namespace ResumableFunctions.Handler.Core.Abstraction
{
    //todo:Candidate for Inbox/Outbox pattern
    public interface IServiceQueue
    {
        Task IdentifyAffectedServices(long pushedCallId, DateTime puhsedCallDate, string methodUrn);
        Task ProcessPushedCall(ImpactedFunctionsIds callImapction);
        Task ProcessPushedCallLocally(long pushedCallId, string methodUrn, DateTime puhsedCallDate);
        Task RoutePushedCallForProcessing(ImpactedFunctionsIds callImapction);
    }
}