using ResumableFunctions.Publisher.Abstraction;
using ResumableFunctions.Publisher.InOuts;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ResumableFunctions.Publisher.Implementation
{
    public class InMemoryFailedRequestRepo : IFailedRequestRepo
    {
        private readonly ConcurrentDictionary<Guid, FailedRequest> _failedRequests = new ConcurrentDictionary<Guid, FailedRequest>();

        public Task Add(FailedRequest request)
        {
            _failedRequests.TryAdd(request.Key, request);
            return Task.CompletedTask;
        }

        public IEnumerable<FailedRequest> GetRequests()
        {
            var enumerator = _failedRequests.GetEnumerator();
            while (enumerator.MoveNext())
                yield return enumerator.Current.Value;
        }

        public Task<bool> HasRequests() => Task.FromResult(_failedRequests.Count > 0);

        public Task Remove(FailedRequest request)
        {
            _failedRequests.TryRemove(request.Key, out _);
            return Task.CompletedTask;
        }

        public Task Update(FailedRequest request) => Task.CompletedTask;
    }
}
