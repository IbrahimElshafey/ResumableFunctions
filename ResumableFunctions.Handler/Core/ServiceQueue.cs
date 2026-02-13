using Microsoft.Extensions.Logging;
using ResumableFunctions.Handler.Abstraction.Abstraction;
using ResumableFunctions.Handler.Core.Abstraction;
using ResumableFunctions.Handler.Helpers;
using ResumableFunctions.Handler.InOuts;
using System.ComponentModel;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ResumableFunctions.Handler.Core;
internal class ServiceQueue : IServiceQueue
{
    private readonly IBackgroundProcess _backgroundJobClient;
    private readonly ILogger<ServiceQueue> _logger;
    private readonly IWaitsProcessor _waitsProcessor;
    private readonly IWaitsRepo _waitsRepository;
    private readonly BackgroundJobExecutor _backgroundJobExecutor;
    private readonly IResumableFunctionsSettings _settings;
    private readonly IScanLocksRepo _lockStateRepo;
    private readonly IHttpClientFactory _httpClientFactory;

    public ServiceQueue(
        ILogger<ServiceQueue> logger,
        IWaitsProcessor waitsProcessor,
        IWaitsRepo waitsRepository,
        IBackgroundProcess backgroundJobClient,
        BackgroundJobExecutor backgroundJobExecutor,
        IResumableFunctionsSettings settings,
        IScanLocksRepo lockStateRepo,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _waitsProcessor = waitsProcessor;
        _waitsRepository = waitsRepository;
        _backgroundJobClient = backgroundJobClient;
        _backgroundJobExecutor = backgroundJobExecutor;
        _settings = settings;
        _lockStateRepo = lockStateRepo;
        _httpClientFactory = httpClientFactory;
    }

    [DisplayName("Identify Impacted Services [PushedCallId: {0},MethodUrn: {2}]")]
    public async Task IdentifyAffectedServices(long pushedCallId, DateTime puhsedCallDate, string methodUrn)
    {
        //if scan is running schedule it for later processing
        if (!await _lockStateRepo.AreLocksExist())
        {
            //get current job id?
            _backgroundJobClient.Schedule(() => IdentifyAffectedServices(
                pushedCallId,
                puhsedCallDate,
                methodUrn), TimeSpan.FromSeconds(3));
            return;
        }

        //no chance to be called by two services in same time, lock removed
        await _backgroundJobExecutor.ExecuteWithoutLock(
            async () =>
            {
                var impactedFunctionsIds = await _waitsRepository.GetImpactedFunctions(methodUrn, puhsedCallDate);
                if (impactedFunctionsIds == null || impactedFunctionsIds.Any() is false)
                {
                    _logger.LogWarning($"There are no services affected by pushed call [{methodUrn}:{pushedCallId}]");
                    return;
                }

                foreach (var callEffection in impactedFunctionsIds)
                {
                    callEffection.CallId = pushedCallId;
                    callEffection.MethodUrn = methodUrn;
                    callEffection.CallDate = puhsedCallDate;
                    var isLocal = callEffection.AffectedServiceId == _settings.CurrentServiceId;
                    if (isLocal)
                        await ProcessPushedCall(callEffection);
                    else
                        await RoutePushedCallForProcessing(callEffection);
                }
            },
            $"Error when call [{nameof(IdentifyAffectedServices)}(pushedCallId:{pushedCallId}, methodUrn:{methodUrn})] in service [{_settings.CurrentServiceId}]");
    }

    [DisplayName("Process call [Id: {0},MethodUrn: {1}] Locally.")]
    public async Task ProcessPushedCallLocally(long pushedCallId, string methodUrn, DateTime puhsedCallDate)
    {
        if (!await _lockStateRepo.AreLocksExist())
        {
            _backgroundJobClient.Schedule(() =>
            ProcessPushedCallLocally(pushedCallId, methodUrn, puhsedCallDate), TimeSpan.FromSeconds(3));
            return;
        }

        //$"{nameof(ProcessCallLocally)}_{pushedCallId}_{_settings.CurrentServiceId}",
        //no chance to be called by two services at same time
        await _backgroundJobExecutor.ExecuteWithoutLock(
            async () =>
            {
                var callEffection = await _waitsRepository.GetCallEffectionInCurrentService(methodUrn, puhsedCallDate);

                if (callEffection != null)
                {
                    callEffection.CallId = pushedCallId;
                    callEffection.MethodUrn = methodUrn;
                    callEffection.CallDate = puhsedCallDate;
                    await ProcessPushedCall(callEffection);
                }
                else
                {
                    _logger.LogWarning($"There are no functions affected in current service by pushed call [{methodUrn}:{pushedCallId}]");
                }
            },
            $"Error when call [{nameof(ProcessPushedCallLocally)}(pushedCallId:{pushedCallId}, methodUrn:{methodUrn})] in service [{_settings.CurrentServiceId}]");
    }

    [DisplayName("{0}")]
    public async Task ProcessPushedCall(ImpactedFunctionsIds callEffection)
    {
        var pushedCallId = callEffection.CallId;
        //$"ServiceProcessPushedCall_{pushedCallId}_{_settings.CurrentServiceId}",
        //todo:lock if there are many service instances
        await _backgroundJobExecutor.ExecuteWithoutLock(
            () =>
            {
                foreach (var functionId in callEffection.AffectedFunctionsIds)
                {
                    _backgroundJobClient.Enqueue(
                        () => _waitsProcessor.FindFunctionMatchedWaits(
                            functionId, pushedCallId, callEffection.MethodGroupId, callEffection.CallDate));
                }
                return Task.CompletedTask;
            },
            $"Error when call [ServiceProcessPushedCall(pushedCallId:{pushedCallId}, methodUrn:{callEffection.MethodUrn})] in service [{_settings.CurrentServiceId}]");
    }

    [DisplayName("[{0}]")]
    public async Task RoutePushedCallForProcessing(ImpactedFunctionsIds callImpaction)
    {
        try
        {
            var actionUrl = $"{callImpaction.AffectedServiceUrl}{Constants.ResumableFunctionsControllerUrl}/{Constants.ServiceProcessPushedCallAction}";
            await DirectHttpPost(actionUrl, callImpaction);// will go to ResumableFunctionsController.ServiceProcessPushedCall action
        }
        catch (Exception)
        {
            _backgroundJobClient.Schedule(() => RoutePushedCallForProcessing(callImpaction), TimeSpan.FromSeconds(3));
        }
    }

    private async Task DirectHttpPost(string actionUrl, ImpactedFunctionsIds callImapction)
    {
        var client = _httpClientFactory.CreateClient();
        var json = JsonSerializer.Serialize(callImapction);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsJsonAsync(actionUrl, content);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync();
        if (!(result == "1" || result == "-1"))
            throw new Exception("Expected result must be 1 or -1");
    }
}