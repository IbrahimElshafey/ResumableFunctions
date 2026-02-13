using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResumableFunctions.Handler.Abstraction.Abstraction;
using ResumableFunctions.Handler.Core.Abstraction;
using ResumableFunctions.Handler.InOuts;
using ResumableFunctions.Handler.InOuts.Entities;

namespace ResumableFunctions.Handler.DataAccess;

internal class WaitTemplatesRepo : IWaitTemplatesRepo
{
    private readonly WaitsDataContext _context;
    private readonly IResumableFunctionsSettings _settings;
    private IServiceProvider _serviceProvider;

    public WaitTemplatesRepo(IServiceProvider serviceProvider, WaitsDataContext context, IResumableFunctionsSettings settings)
    {
        _settings = settings;
        _context = context;
        _serviceProvider = serviceProvider;
        _serviceProvider.GetService<WaitsDataContext>();
    }

    public async Task<WaitTemplate> AddNewTemplate(byte[] hashResult, MethodWaitEntity methodWait)
    {
        return await AddNewTemplate(
            hashResult,
            methodWait.CurrentFunction,
            methodWait.RequestedByFunctionId,
            methodWait.MethodGroupToWaitId,
            methodWait.MethodToWaitId,
            methodWait.InCodeLine,
            methodWait.CancelMethodAction,
            methodWait.AfterMatchAction,
            methodWait.MatchExpressionParts
            );
    }

    public async Task<WaitTemplate> CheckTemplateExist(byte[] hash, int funcId, int groupId)
    {
        var waitTemplate = (await
            _context.WaitTemplates
            .Where(x =>
                x.MethodGroupId == groupId &&
                x.FunctionId == funcId &&
                x.ServiceId == _settings.CurrentServiceId)
            .ToListAsync())
            .FirstOrDefault(x => x.Hash.SequenceEqual(hash));
        if (waitTemplate != null)
        {
            waitTemplate.LoadUnmappedProps();
            if (waitTemplate.IsActive == -1)
            {
                waitTemplate.IsActive = 1;
                await _context.SaveChangesDirectly();
            }
        }
        return waitTemplate;
    }


    public async Task<List<WaitTemplate>> GetWaitTemplatesForFunction(int methodGroupId, int functionId)
    {
        var waitTemplatesQry = _context
            .WaitTemplates
            .Where(template =>
                template.FunctionId == functionId &&
                template.MethodGroupId == methodGroupId &&
                template.ServiceId == _settings.CurrentServiceId &&
                template.IsActive == 1);

        var result = await
            waitTemplatesQry
            .OrderByDescending(x => x.Id)
            .AsNoTracking()
            .ToListAsync();

        result.ForEach(x => x.LoadUnmappedProps());
        return result;
    }


    public async Task<WaitTemplate> GetById(int templateId)
    {
        var waitTemplate = await _context.WaitTemplates.FindAsync(templateId);
        waitTemplate?.LoadUnmappedProps();
        return waitTemplate;
    }

    public async Task<WaitTemplate> GetWaitTemplateWithBasicMatch(int methodWaitTemplateId)
    {
        var template =
            await _context
            .WaitTemplates
            .Select(waitTemplate =>
                new WaitTemplate
                {
                    MatchExpressionValue = waitTemplate.MatchExpressionValue,
                    AfterMatchAction = waitTemplate.AfterMatchAction,
                    Id = waitTemplate.Id,
                    FunctionId = waitTemplate.FunctionId,
                    MethodId = waitTemplate.MethodId,
                    MethodGroupId = waitTemplate.MethodGroupId,
                    ServiceId = waitTemplate.ServiceId,
                    IsActive = waitTemplate.IsActive,
                    CancelMethodAction = waitTemplate.CancelMethodAction,
                })
            .FirstAsync(x => x.Id == methodWaitTemplateId);
        template.LoadUnmappedProps();
        return template;
    }

    public async Task<WaitTemplate> AddNewTemplate(
        byte[] hashResult,
        object currentFunctionInstance,
        int funcId,
        int groupId,
        int? methodId,
        int inCodeLine,
        string cancelMethodAction,
        string afterMatchAction,
        MatchExpressionParts matchExpressionParts
        )
    {
        var scope = _serviceProvider.CreateScope();
        var tempContext = scope.ServiceProvider.GetService<WaitsDataContext>();
        var waitTemplate = new WaitTemplate
        {
            MethodId = methodId,
            FunctionId = funcId,
            MethodGroupId = groupId,
            Hash = hashResult,
            InCodeLine = inCodeLine,
            IsActive = 1,
            CancelMethodAction = cancelMethodAction,
            AfterMatchAction = afterMatchAction
        };

        if (matchExpressionParts != null)
        {
            waitTemplate.MatchExpression = matchExpressionParts.MatchExpression;
            waitTemplate.CallMandatoryPartExpression = matchExpressionParts.CallMandatoryPartExpression;
            waitTemplate.InstanceMandatoryPartExpression = matchExpressionParts.InstanceMandatoryPartExpression;
            waitTemplate.IsMandatoryPartFullMatch = matchExpressionParts.IsMandatoryPartFullMatch;
        }

        tempContext.WaitTemplates.Add(waitTemplate);

        await tempContext.SaveChangesAsync();
        //reattach to current context
        _context.Attach(waitTemplate);
        return waitTemplate;
    }
}
