using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ResumableFunctions.Handler.UiService;
using ResumableFunctions.MvcUi.DisplayObject;

namespace ResumableFunctions.MvcUi.Areas.RF.Controllers
{
    [Area("RF")]
    public class FunctionInstancesController : Controller
    {
        private readonly ILogger<FunctionInstancesController> _logger;
        private readonly IUiService _uiService;

        public FunctionInstancesController(ILogger<FunctionInstancesController> logger, IUiService uiService)
        {
            _logger = logger;
            _uiService = uiService;
        }
        public async Task<IActionResult> AllInstances(int functionId, string functionName)
        {
            return View(
                new FunctionInstancesModel
                {
                    FunctionName = functionName,
                    Instances = await _uiService.GetFunctionInstances(functionId)
                });
        }

        public async Task<IActionResult> FunctionInstance(int instanceId)
        {
            return View(await _uiService.GetFunctionInstanceDetails(instanceId));
        }

    }
}