using Microsoft.Extensions.Logging;
using ResumableFunctions.Publisher.Abstraction;
using ResumableFunctions.Publisher.InOuts;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
namespace ResumableFunctions.Publisher.Implementation
{
    public class FailedRequestHandler : IFailedRequestHandler
    {
        private readonly IPublisherSettings _settings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<FailedRequestHandler> _logger;
        private readonly IFailedRequestRepo _failedRequestRepo;

        public FailedRequestHandler(
           IPublisherSettings settings,
           IHttpClientFactory httpClientFactory,
           ILogger<FailedRequestHandler> logger,
           IFailedRequestRepo failedRequestRepo)
        {
            _settings = settings;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _failedRequestRepo = failedRequestRepo;
        }
        public async Task EnqueueFailedRequest(FailedRequest failedRequest)
        {
            try
            {
                await _failedRequestRepo.Add(failedRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Can't save failed request.");
            }
        }

        public async Task HandleFailedRequests()
        {
            while (true)
            {
                await Task.Delay(_settings.CheckFailedRequestEvery);
                if (await _failedRequestRepo.HasRequests())
                    await CallFailedRequests();
            }
        }

        // ReSharper disable once FunctionRecursiveOnAllPaths
        private async Task CallFailedRequests()
        {
            try
            {
                _logger.LogInformation("Start handling failed requests.");
                var requestsTasks = new List<Task>();
                foreach (var request in _failedRequestRepo.GetRequests())
                {
                    requestsTasks.Add(CallRequest(request));
                }
                await Task.WhenAll(requestsTasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when handling failed requests.");
            }
        }

        private async Task CallRequest(FailedRequest request)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var response =
                    await client.PostAsync(request.ActionUrl, new ByteArrayContent(request.Body));
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadAsStringAsync();
                if (result == "1" || result == "-1")
                    await _failedRequestRepo.Remove(request);
                else
                    throw new Exception("Expected result must be 1 or -1");
            }
            catch (Exception ex)
            {
                if (request != null)
                {
                    request.AttemptsCount++;
                    _logger.LogError(ex,
                        $"A request [{request.Key}] failed again for [{request.AttemptsCount}] times");
                    request.LastAttemptDate = DateTime.UtcNow;
                    await _failedRequestRepo.Update(request);
                }
            }
        }
    }
}
