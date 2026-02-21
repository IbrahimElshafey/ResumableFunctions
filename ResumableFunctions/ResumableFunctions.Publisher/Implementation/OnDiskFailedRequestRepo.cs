using MessagePack.Resolvers;
using MessagePack;
using ResumableFunctions.Publisher.Abstraction;
using ResumableFunctions.Publisher.InOuts;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System;
using System.Linq;

namespace ResumableFunctions.Publisher.Implementation
{
    public class OnDiskFailedRequestRepo : IFailedRequestRepo
    {
        const string requestsFolder = ".\\FailedRequests";
        private readonly ConcurrentDictionary<Guid, FailedRequest> _failedRequests = new ConcurrentDictionary<Guid, FailedRequest>();
        public OnDiskFailedRequestRepo()
        {
            //todo: add logger and settings
            //settings will be (Failed Requests folder path,Wait for save on disk or fire and forget)
            Directory.CreateDirectory(requestsFolder);
        }
        public async Task Add(FailedRequest request)
        {
            await WriteRequest(request);
            _failedRequests.TryAdd(request.Key, request);
        }

        public IEnumerable<FailedRequest> GetRequests()
        {
            //if no in memory data , read from disk
            if (_failedRequests.Count == 0)
            {
                foreach (var file in Directory.EnumerateFiles(requestsFolder))
                {
                    byte[] buffer = File.ReadAllBytes(file);
                    var request = MessagePackSerializer.Deserialize<FailedRequest>(buffer, ContractlessStandardResolver.Options);
                    _failedRequests.TryAdd(request.Key, request);
                }
            }
            var enumerator = _failedRequests.GetEnumerator();
            while (enumerator.MoveNext())
                yield return enumerator.Current.Value;
        }

        public Task<bool> HasRequests()
        {
            //list has items or folder has files
            return Task.FromResult(_failedRequests.Count > 0 || Directory.EnumerateFiles(requestsFolder).Any());
        }

        public Task Remove(FailedRequest request)
        {
            if (File.Exists(FilePath(request)))
                File.Delete(FilePath(request));
            _failedRequests.TryRemove(request.Key, out _);
            return Task.CompletedTask;
        }

        public async Task Update(FailedRequest request) => await WriteRequest(request);

        private string FilePath(FailedRequest request) => $"{requestsFolder}\\{request.Key}.file";

        private async Task WriteRequest(FailedRequest request)
        {
            var byteArray = MessagePackSerializer.Serialize(request, ContractlessStandardResolver.Options);
            using (FileStream fileStream =
                new FileStream(FilePath(request), FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
            {
                // Write the byte array to the file asynchronously
                await fileStream.WriteAsync(byteArray, 0, byteArray.Length);
            }
        }
    }
}
