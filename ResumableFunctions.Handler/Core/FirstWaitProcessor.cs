using Microsoft.Extensions.Logging;
using ResumableFunctions.Handler.Abstraction.Abstraction;
using ResumableFunctions.Handler.Core.Abstraction;
using ResumableFunctions.Handler.Helpers;
using ResumableFunctions.Handler.InOuts;
using ResumableFunctions.Handler.InOuts.Entities;
using System.ComponentModel;
using System.Reflection;

namespace ResumableFunctions.Handler.Core;

internal class FirstWaitProcessor : IFirstWaitProcessor
{
    private readonly ILogger<FirstWaitProcessor> _logger;
    private readonly IUnitOfWork _context;
    private readonly IMethodIdsRepo _methodIdentifierRepo;
    private readonly IWaitsRepo _waitsRepository;
    private readonly IWaitTemplatesRepo _templatesRepo;
    private readonly IServiceProvider _serviceProvider;
    private readonly BackgroundJobExecutor _backgroundJobExecutor;
    private readonly IBackgroundProcess _backgroundJobClient;
    private readonly ILogsRepo _logsRepo;
    private readonly IScanLocksRepo _scanStateRepo;

    public FirstWaitProcessor(
        ILogger<FirstWaitProcessor> logger,
        IUnitOfWork context,
        IServiceProvider serviceProvider,
        IMethodIdsRepo methodIdentifierRepo,
        IWaitsRepo waitsRepository,
        BackgroundJobExecutor backgroundJobExecutor,
        IBackgroundProcess backgroundJobClient,
        ILogsRepo logsRepo,
        IWaitTemplatesRepo templatesRepo,
        IScanLocksRepo scanStateRepo)
    {
        _logger = logger;
        _context = context;
        _serviceProvider = serviceProvider;
        _methodIdentifierRepo = methodIdentifierRepo;
        _waitsRepository = waitsRepository;
        _backgroundJobExecutor = backgroundJobExecutor;
        _backgroundJobClient = backgroundJobClient;
        _logsRepo = logsRepo;
        _templatesRepo = templatesRepo;
        _scanStateRepo = scanStateRepo;
    }

    public async Task<MethodWaitEntity> CloneFirstWait(MethodWaitEntity firstMatchedMethodWait)
    {
        ResumableFunctionIdentifier resumableFunction = null;
        try
        {
            resumableFunction = await _methodIdentifierRepo.GetResumableFunction(firstMatchedMethodWait.RootFunctionId);
            var firstWaitClone = await GetFirstWait(resumableFunction.MethodInfo, false);
            firstWaitClone.ActionOnChildrenTree(waitClone =>
            {
                waitClone.Status = WaitStatus.Temp;
                waitClone.IsFirst = false;
                waitClone.WasFirst = true;
                waitClone.FunctionState.StateObject = firstMatchedMethodWait?.FunctionState?.StateObject;
                if (waitClone is TimeWaitEntity timeWait)
                {
                    timeWait.TimeWaitMethod.ExtraData.JobId = _backgroundJobClient.Schedule(
                        () => new LocalRegisteredMethods().TimeWait(
                        new TimeWaitInput
                        {
                            TimeMatchId = firstMatchedMethodWait.MandatoryPart,
                            RequestedByFunctionId = firstMatchedMethodWait.RequestedByFunctionId,
                            Description = $"[{timeWait.Name}] in function [{firstMatchedMethodWait.RequestedByFunction.RF_MethodUrn}:{firstMatchedMethodWait.FunctionState.Id}]"
                        }), timeWait.TimeToWait);
                    timeWait.TimeWaitMethod.MandatoryPart = firstMatchedMethodWait.MandatoryPart;
                    timeWait.IgnoreJobCreation = true;
                }

            });

            firstWaitClone.FunctionState.Logs.AddRange(firstWaitClone.FunctionState.Logs);
            firstWaitClone.FunctionState.Status =
                firstWaitClone.FunctionState.HasErrors() ?
                FunctionInstanceStatus.InError :
                FunctionInstanceStatus.InProgress;
            await _waitsRepository.SaveWait(firstWaitClone);//first wait clone

            //return method wait that 
            var currentMatchedMw = firstWaitClone.GetChildMethodWait(firstMatchedMethodWait.Name);
            currentMatchedMw.Input = firstMatchedMethodWait.Input;
            currentMatchedMw.Output = firstMatchedMethodWait.Output;
            var waitTemplate = await _templatesRepo.GetWaitTemplateWithBasicMatch(firstMatchedMethodWait.TemplateId);
            currentMatchedMw.TemplateId = waitTemplate.Id;
            currentMatchedMw.Template = waitTemplate;
            currentMatchedMw.IsFirst = false;
            currentMatchedMw.LoadExpressions();
            await _context.CommitAsync();
            firstWaitClone.ActionOnChildrenTree(waitClone => waitClone.Status = WaitStatus.Waiting);
            return currentMatchedMw;
        }
        catch (Exception ex)
        {
            var error = $"Error when try to clone first wait for function [{resumableFunction?.RF_MethodUrn}]";
            await _logsRepo.AddErrorLog(ex, error, StatusCodes.FirstWait);
            throw new Exception(error, ex);
        }
    }

    [DisplayName("Register First Wait for Function [{0},{1}]")]
    public async Task RegisterFirstWait(int functionId, string methodUrn)
    {
        MethodInfo resumableFunction = null;
        var functionName = "";
        string firstWaitLock = $"FirstWaitProcessor_RegisterFirstWait_{functionId}";
        int firstWaitLockId = -1;
        firstWaitLockId = await _scanStateRepo.AddLock(firstWaitLock);
        try
        {
            await _backgroundJobExecutor.ExecuteWithLock(
            $"FirstWaitProcessor_RegisterFirstWait_{functionId}",//may many services instances
            async () =>
            {
                try
                {
                    var resumableFunctionId = await _methodIdentifierRepo.GetResumableFunction(functionId);
                    methodUrn = resumableFunctionId.RF_MethodUrn;
                    resumableFunction = resumableFunctionId.MethodInfo;
                    functionName = resumableFunction.Name;
                    _logger.LogInformation($"Trying Start Resumable Function [{resumableFunctionId.RF_MethodUrn}] And Register First Wait");
                    var firstWait = await GetFirstWait(resumableFunction, true);

                    if (firstWait != null)
                    {
                        await _logsRepo.AddLog(
                            $"[{resumableFunction.GetFullName()}] started and wait [{firstWait.Name}] to match.", LogType.Info, StatusCodes.FirstWait);

                        await _waitsRepository.SaveWait(firstWait);
                        _logger.LogInformation(
                            $"Save first wait [{firstWait.Name}] for function [{resumableFunction.GetFullName()}].");
                        await _context.CommitAsync();
                    }
                }
                catch (Exception ex)
                {
                    if (resumableFunction != null)
                        await _logsRepo.AddErrorLog(ex, ErrorMsg(), StatusCodes.FirstWait);

                    await _waitsRepository.RemoveFirstWaitIfExist(functionId);
                    throw;
                }

            },
            ErrorMsg());
        }
        finally
        {
            if (firstWaitLockId > -1)
                await _scanStateRepo.RemoveLock(firstWaitLockId);
        }
        string ErrorMsg() => $"Error when try to register first wait for function [{functionName}:{functionId}]";
    }


    public async Task<WaitEntity> GetFirstWait(MethodInfo resumableFunction, bool removeIfExist)
    {
        try
        {
            //todo: ResumableFunctionsContainer must be constructor less if you want to pass dependancies create a method `SetDependencies`
            var classInstance = (ResumableFunctionsContainer)Activator.CreateInstance(resumableFunction.DeclaringType);

            if (classInstance == null)
            {
                var errorMsg = $"Can't initiate a new instance of [{resumableFunction.DeclaringType.FullName}]";
                await _logsRepo.AddErrorLog(null, errorMsg, StatusCodes.FirstWait);

                throw new NullReferenceException(errorMsg);
            }

            classInstance.InitializeDependencies(_serviceProvider);
            classInstance.CurrentResumableFunction = resumableFunction;
            var functionRunner = new FunctionRunner(classInstance, resumableFunction);
            //if (functionRunner.ResumableFunctionExistInCode is false)
            //{
            //    var message = $"Resumable function ({resumableFunction.GetFullName()}) not exist in code.";
            //    _logger.LogWarning(message);
            //    await _logsRepo.AddErrorLog(null, message, StatusCodes.FirstWait);
            //    throw new NullReferenceException(message);
            //}

            await functionRunner.MoveNextAsync();
            var firstWait = functionRunner.CurrentWaitEntity;

            if (firstWait == null)
            {
                await _logsRepo.AddErrorLog(
                    null,
                    $"Can't get first wait in function [{resumableFunction.GetFullName()}].",
                    StatusCodes.FirstWait);
                return null;
            }

            var functionId = await _methodIdentifierRepo.GetResumableFunction(new MethodData(resumableFunction));
            if (removeIfExist)
            {
                _logger.LogInformation("First wait already exist it will be deleted and recreated since it may be changed.");
                await _waitsRepository.RemoveFirstWaitIfExist(functionId.Id);
            }
            var functionState = new ResumableFunctionState
            {
                ResumableFunctionIdentifier = functionId,
                StateObject = classInstance,
            };
            firstWait.ActionOnChildrenTree(x =>
            {
                x.RequestedByFunction = functionId;
                x.RequestedByFunctionId = functionId.Id;
                x.IsFirst = true;
                x.WasFirst = true;
                x.RootFunctionId = functionId.Id;
                x.FunctionState = functionState;
            });
            return firstWait;
        }
        catch (Exception ex)
        {
            await _logsRepo.AddErrorLog(ex, "Error when get first wait.", StatusCodes.FirstWait);
            throw;
        }
    }
}