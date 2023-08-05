﻿using System.ComponentModel;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ResumableFunctions.Handler.Attributes;
using ResumableFunctions.Handler.Core.Abstraction;
using ResumableFunctions.Handler.DataAccess;
using ResumableFunctions.Handler.DataAccess.Abstraction;
using ResumableFunctions.Handler.Helpers;
using ResumableFunctions.Handler.InOuts;

namespace ResumableFunctions.Handler.Core;

internal class Scanner
{
    private readonly IUnitOfWork _context;
    private readonly IResumableFunctionsSettings _settings;
    private readonly ILogger<Scanner> _logger;
    private readonly IMethodIdsRepo _methodIdentifierRepo;
    private readonly IWaitsRepo _waitsRepository;
    private readonly IFirstWaitProcessor _firstWaitProcessor;
    private readonly IBackgroundProcess _backgroundJobClient;
    private readonly string _currentServiceName;
    private readonly BackgroundJobExecutor _backgroundJobExecutor;
    private readonly IServiceRepo _serviceRepo;
    private HashSet<string> _functionsUrns = new HashSet<string>();

    public Scanner(
        ILogger<Scanner> logger,
        IMethodIdsRepo methodIdentifierRepo,
        IFirstWaitProcessor firstWaitProcessor,
        IResumableFunctionsSettings settings,
        IUnitOfWork context,
        IBackgroundProcess backgroundJobClient,
        IWaitsRepo waitsRepository,
        BackgroundJobExecutor backgroundJobExecutor,
        IServiceRepo serviceRepo)
    {
        _logger = logger;
        _methodIdentifierRepo = methodIdentifierRepo;
        _firstWaitProcessor = firstWaitProcessor;
        _settings = settings;
        _context = context;
        _backgroundJobClient = backgroundJobClient;
        _waitsRepository = waitsRepository;
        _currentServiceName = Assembly.GetEntryAssembly().GetName().Name;
        _backgroundJobExecutor = backgroundJobExecutor;
        _serviceRepo = serviceRepo;
    }

    [DisplayName("Start Scanning Current Service")]
    public async Task Start()
    {
        await _backgroundJobExecutor.Execute(
            $"Scanner_ScanningService_{_currentServiceName}",
            async () =>
            {
                await RegisterMethods(GetAssembliesToScan());

                await RegisterMethodsInType(typeof(LocalRegisteredMethods), null);

                await _context.SaveChangesAsync();
            },
            $"Error when scan [{_currentServiceName}]", true);

    }

    private List<string> GetAssembliesToScan()
    {
        var currentFolder = AppContext.BaseDirectory;
        _logger.LogInformation($"Get assemblies to scan in directory [{currentFolder}].");
        var assemblyPaths = new List<string>
            {
                $"{currentFolder}{_currentServiceName}.dll"
            };
        if (_settings.DllsToScan != null)
            assemblyPaths.AddRange(_settings.DllsToScan.Select(x => $"{currentFolder}{x}.dll"));

        assemblyPaths = assemblyPaths.Distinct().ToList();
        return assemblyPaths;
    }

    internal BindingFlags GetBindingFlags() =>
        BindingFlags.DeclaredOnly |
        BindingFlags.Public |
        BindingFlags.NonPublic |
        BindingFlags.Static |
        BindingFlags.Instance;

    internal async Task RegisterResumableFunction(MethodInfo resumableFunctionMInfo, ServiceData serviceData)
    {
        var info = await GetFunctionInfo(resumableFunctionMInfo, serviceData);
        if (info.FunctionData is not null)
        {
            _functionsUrns.Add(info.FunctionData.MethodUrn);
            var resumableFunctionIdentifier = await _methodIdentifierRepo.AddResumableFunctionIdentifier(info.FunctionData);
            await _context.SaveChangesAsync();
            if (info.RegisterFirstWait)
                _backgroundJobClient.Enqueue(
                       () => _firstWaitProcessor.RegisterFirstWait(resumableFunctionIdentifier.Id));
            if (info.RemoveFirstWait)
                await _waitsRepository.RemoveFirstWaitIfExist(resumableFunctionIdentifier.Id);
        }
    }

    private async Task<(MethodData FunctionData, bool RegisterFirstWait, bool RemoveFirstWait)> GetFunctionInfo(
        MethodInfo resumableFunctionMInfo, ServiceData serviceData)
    {
        var entryPointCheck = EntryPointCheck(resumableFunctionMInfo);
        var methodType = entryPointCheck.IsEntry ? MethodType.ResumableFunctionEntryPoint : MethodType.SubResumableFunction;
        serviceData.AddLog($"Register resumable function [{resumableFunctionMInfo.GetFullName()}] of type [{methodType}]", LogType.Info, StatusCodes.Scanning);
        var functionData = new MethodData(resumableFunctionMInfo)
        {
            MethodType = methodType,
            IsActive = entryPointCheck.IsActive
        };
        if (_functionsUrns.Contains(functionData.MethodUrn))
        {
            await _serviceRepo.AddErrorLog(null,
                $"Can't add method identifier for function [{resumableFunctionMInfo.GetFullName()}]" +
                $" since same URN [{functionData.MethodUrn}] used for another function.", StatusCodes.MethodValidation);
            return (null, false, false);
        }
        var registerFirstWait = entryPointCheck.IsActive && entryPointCheck.IsEntry;
        var removeFirstWait = !entryPointCheck.IsActive && entryPointCheck.IsEntry;
        return (functionData, registerFirstWait, removeFirstWait);
    }

    private async Task RegisterMethods(List<string> assemblyPaths)
    {
        var resumableFunctionClasses = new List<Type>();
        foreach (var assemblyPath in assemblyPaths)
        {
            try
            {
                //check if file exist
                _logger.LogInformation($"Start scan assembly [{assemblyPath}]");

                var dateBeforeScan = DateTime.Now;
                if (await _serviceRepo.ShouldScanAssembly(assemblyPath) is false) continue;


                var assembly = Assembly.LoadFile(assemblyPath);
                var serviceData = await _serviceRepo.GetServiceData(assembly.GetName().Name);

                foreach (var type in assembly.GetTypes())
                {
                    await RegisterMethodsInType(type, serviceData);
                    //await RegisterExternalMethods(type);
                    if (type.IsSubclassOf(typeof(ResumableFunctionsContainer)))
                        resumableFunctionClasses.Add(type);
                }

                _logger.LogInformation($"Save discovered method waits for assembly [{assemblyPath}].");
                await _context.SaveChangesAsync();

                foreach (var resumableFunctionClass in resumableFunctionClasses)
                    await RegisterResumableFunctionsInClass(resumableFunctionClass);

                await _serviceRepo.UpdateDllScanDate(serviceData);
                await _serviceRepo.DeleteOldScanData(dateBeforeScan);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error when register a method in assembly [{assemblyPath}]");
                throw;
            }
        }
    }

    internal async Task RegisterMethodsInType(Type type, ServiceData serviceData)
    {
        try
        {
            var urns = new List<string>();
            var methodWaits = type
                .GetMethods(GetBindingFlags())
                .Where(method =>
                        method.GetCustomAttributes().Any(x => x is PushCallAttribute));
            foreach (var method in methodWaits)
            {
                if (ValidateMethod(method, serviceData))
                {
                    var methodData = new MethodData(method) { MethodType = MethodType.MethodWait };

                    if (UrnDuplication(methodData.MethodUrn, method.GetFullName())) continue;

                    await _methodIdentifierRepo.AddWaitMethodIdentifier(methodData);
                    serviceData?.AddLog($"Adding method identifier {methodData}", LogType.Info, StatusCodes.Scanning);
                }
                else
                    serviceData?.AddError(
                        $"Can't add method identifier [{method.GetFullName()}] since it does not match the criteria.", StatusCodes.MethodValidation, null);
            }

            bool UrnDuplication(string methodUrn, string methodName)
            {
                if (urns.Contains(methodUrn))
                {
                    serviceData?.AddError(
                    $"Can't add method identifier [{methodName}] since same URN [{methodUrn}] used for another method in same class.", StatusCodes.MethodValidation, null);
                    return true;
                }
                else
                {
                    urns.Add(methodUrn);
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error when adding a method identifier of type [MethodWait] in type [{type.FullName}]";
            serviceData?.AddError(errorMsg, StatusCodes.Scanning, ex);
            _logger.LogError(ex, errorMsg);
            throw;
        }
    }

    private bool ValidateMethod(MethodInfo method, ServiceData serviceData)
    {
        var result = true;
        if (method.IsGenericMethod)
        {
            serviceData?.AddError($"[{method.GetFullName()}] must not be generic.", StatusCodes.MethodValidation, null);
            result = false;
        }
        if (method.ReturnType == typeof(void))
        {
            serviceData?.AddError($"[{method.GetFullName()}] must return a value, void is not allowed.", StatusCodes.MethodValidation, null);
            result = false;
        }
        if (method.IsAsyncMethod() && method.ReturnType.GetGenericTypeDefinition() != typeof(Task<>))
        {
            serviceData?.AddError($"[{method.GetFullName()}] async method must return Task<T> object.", StatusCodes.MethodValidation, null);
            result = false;
        }
        if (method.IsStatic)
        {
            serviceData?.AddError($"[{method.GetFullName()}] must be instance method.", StatusCodes.MethodValidation, null);
            result = false;
        }
        if (method.GetParameters().Length != 1)
        {
            serviceData?.AddError($"[{method.GetFullName()}] must have only one parameter.", StatusCodes.MethodValidation, null);
            result = false;
        }
        return result;
    }

    internal async Task RegisterResumableFunctionsInClass(Type type)
    {

        var serviceData = await _serviceRepo.GetServiceData(type.Assembly.GetName().Name);

        CheckSetDependenciesMethodExist(type, serviceData);
        serviceData.AddLog($"Try to find resumable functions in type [{type.FullName}]", LogType.Info, StatusCodes.Scanning);

        var hasCtorLess = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, Type.EmptyTypes, null) == null;
        if (hasCtorLess)
        {
            serviceData.AddError($"You must define parameter-less constructor for type [{type.FullName}] to enable serialization for it.", StatusCodes.Scanning, null);
            return;
        }

        await RegisterFunctions(typeof(SubResumableFunctionAttribute), type, serviceData);
        await _context.SaveChangesAsync();
        await RegisterFunctions(typeof(ResumableFunctionEntryPointAttribute), type, serviceData);
    }


    internal async Task RegisterFunctions(Type attributeType, Type type, ServiceData serviceData)
    {
        var urns = new List<string>();
        var functions = type
            .GetMethods(GetBindingFlags())
            .Where(method => method
                .GetCustomAttributes()
                .Any(attribute => attribute.GetType() == attributeType));

        foreach (var resumableFunctionInfo in functions)
        {
            if (ValidateResumableFunctionSignature(resumableFunctionInfo, serviceData))
                await RegisterResumableFunction(resumableFunctionInfo, serviceData);
            else
                serviceData.AddError($"Can't register resumable function [{resumableFunctionInfo.GetFullName()}].", StatusCodes.MethodValidation, null);
        }
    }
    private void CheckSetDependenciesMethodExist(Type type, ServiceData serviceData)
    {

        var setDependenciesMi = type.GetMethod(
            "SetDependencies", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (setDependenciesMi != null) return;

        serviceData.AddLog(
            $"No instance method like [void SetDependencies(Interface dep1,...)] found in class [{type.FullName}] that set your dependencies.",
            LogType.Warning, StatusCodes.Scanning);
    }


    private (bool IsEntry, bool IsActive) EntryPointCheck(MethodInfo resumableFunction)
    {
        var resumableFunctionAttribute =
            (ResumableFunctionEntryPointAttribute)resumableFunction.GetCustomAttributes()
            .FirstOrDefault(attribute => attribute is ResumableFunctionEntryPointAttribute);
        var isFunctionActive = resumableFunctionAttribute is { IsActive: true };
        return (resumableFunctionAttribute != null, isFunctionActive);
    }

    internal bool ValidateResumableFunctionSignature(MethodInfo resumableFunction, ServiceData serviceData)
    {
        var errors = new List<string>();
        if (!resumableFunction.IsAsyncMethod())
            errors.Add($"The resumable function [{resumableFunction.GetFullName()}] must be async.");

        if (resumableFunction.ReturnType != typeof(IAsyncEnumerable<Wait>) || resumableFunction.GetParameters().Length != 0)
            errors.Add(
                $"The resumable function [{resumableFunction.GetFullName()}] must match the signature [IAsyncEnumerable<Wait> {resumableFunction.Name}()].\n" +
                $"Must have no parameter and return type must be [IAsyncEnumerable<Wait>]");

        if (resumableFunction.IsStatic)
            errors.Add($"Resumable function [{resumableFunction.GetFullName()}] must be instance method.");

        var hasOverloads = resumableFunction.DeclaringType.GetMethods(GetBindingFlags()).Count(x => x.Name == resumableFunction.Name) > 1;
        if (hasOverloads)
            errors.Add($"The resumable function [{resumableFunction.Name}] must not overloaded, just declare one method with the name [{resumableFunction.Name}].");

        if (errors.Any())
        {
            errors.ForEach(errorMsg => serviceData.AddError(errorMsg, StatusCodes.MethodValidation, null));
            return false;
        }
        return true;
    }


}

