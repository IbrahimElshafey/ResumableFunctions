﻿using Microsoft.EntityFrameworkCore;
using ResumableFunctions.Handler.DataAccess.Abstraction;
using ResumableFunctions.Handler.InOuts;
using System.Reflection;
using System.Runtime;
using Microsoft.Extensions.Logging;

namespace ResumableFunctions.Handler.DataAccess;

public class ServiceRepo : IServiceRepo
{
    private readonly FunctionDataContext _context;
    private readonly IResumableFunctionsSettings _settings;
    private readonly ILogger<ServiceRepo> _logger;

    public ServiceRepo(
        FunctionDataContext context,
        IResumableFunctionsSettings settings,
        ILogger<ServiceRepo> logger)
    {
        _context = context;
        _settings = settings;
        _logger = logger;
    }

    public async Task UpdateDllScanDate(ServiceData dll)
    {
        await _context.Entry(dll).ReloadAsync();
        dll.AddLog($"Update last scan date for service [{dll.AssemblyName}] to [{DateTime.Now}].");
        dll.Modified = DateTime.Now;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteOldScanData(DateTime dateBeforeScan)
    {
        await _context
            .Logs
            .Where(x =>
                x.EntityId == _settings.CurrentServiceId &&
                x.EntityType == nameof(ServiceData) &&
                x.Created < dateBeforeScan)
            .ExecuteDeleteAsync();
    }

    public async Task<bool> ShouldScanAssembly(string assemblyPath)
    {
        var currentAssemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
        var serviceData = await _context.ServicesData.FirstOrDefaultAsync(x => x.AssemblyName == currentAssemblyName);

        if (serviceData == null)
        {
            serviceData = await AddNewServiceData(currentAssemblyName);
        }
        else if (serviceData.ParentId != _settings.CurrentServiceId)
        {
            var rootService = _context.ServicesData.Local.FirstOrDefault(x => x.Id == _settings.CurrentServiceId);
            rootService?.AddError($"Dll `{currentAssemblyName}` will not be added to this service because it's used in another service.");
            return false;
        }

        _settings.CurrentServiceId = serviceData.ParentId == -1 ? serviceData.Id : serviceData.ParentId;
        if (File.Exists(assemblyPath) is false)
        {
            var message = $"Assembly file ({assemblyPath}) not exist.";
            _logger.LogError(message);
            serviceData.AddError(message);
            return false;
        }

        serviceData.ErrorCounter = 0;

        if (serviceData.ParentId == -1)
        {
            await _context
               .ServicesData
               .Where(x => x.ParentId == serviceData.Id)
               .ExecuteDeleteAsync();
        }


        var assembly = Assembly.LoadFile(assemblyPath);
        var isReferenceResumableFunction =
            assembly.GetReferencedAssemblies().Any(x => new[]
            {
                "ResumableFunctions.Handler",
                "ResumableFunctions.AspNetService"
            }.Contains(x.Name));

        if (isReferenceResumableFunction is false)
        {
            serviceData.AddError($"No reference for ResumableFunction DLLs found,The scan canceled for [{assemblyPath}].");
            return false;
        }

        var lastBuildDate = File.GetLastWriteTime(assemblyPath);
        serviceData.Url = _settings.CurrentServiceUrl;
        serviceData.AddLog($"Check last scan date for assembly [{currentAssemblyName}].");
        var shouldScan = lastBuildDate > serviceData.Modified;
        if (shouldScan is false)
            serviceData.AddLog($"No need to rescan assembly [{currentAssemblyName}].");
        if (_settings.ForceRescan)
            serviceData.AddLog("Will be scanned because force rescan is enabled in Debug mode.", LogType.Warning);
        return shouldScan || _settings.ForceRescan;
    }

    public async Task<ServiceData> GetServiceData(string assemblyName)
    {
        return await _context.ServicesData.FirstOrDefaultAsync(x => x.AssemblyName == assemblyName);
    }

    private async Task<ServiceData> AddNewServiceData(string currentAssemblyName)
    {
        var parentId = _settings.CurrentServiceId;
        var newServiceData = new ServiceData
        {
            AssemblyName = currentAssemblyName,
            Url = _settings.CurrentServiceUrl,
            ParentId = parentId
        };
        _context.ServicesData.Add(newServiceData);
        newServiceData.AddLog($"Assembly [{currentAssemblyName}] will be scanned.");
        await _context.SaveChangesAsync();
        return newServiceData;
    }

}