using ResumableFunctions.Publisher.InOuts;
using System;
using System.Threading.Tasks;

namespace ResumableFunctions.Publisher.Abstraction
{
    public interface ICallPublisher
    {
        //todo: Candidate for Inbox/Outbox pattern
        //todo: this will be seprated to class library
        //we can use this interface to publish the method call to the queue
        //the queue will be consumed by the worker
        Task Publish<TInput, TOutput>(
            Func<TInput, Task<TOutput>> methodToPush,
            TInput input,
            TOutput output,
            string methodUrn,
            params string[] toServices);
        Task Publish(MethodCall MethodCall);
    }
}
