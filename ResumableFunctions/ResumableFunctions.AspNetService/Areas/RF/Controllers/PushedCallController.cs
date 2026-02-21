using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ResumableFunctions.Handler.UiService;

namespace ResumableFunctions.MvcUi.Areas.RF.Controllers
{
    [Area("RF")]
    public class PushedCallController : Controller
    {
        private readonly ILogger<PushedCallController> _logger;
        private readonly IUiService _service;

        public PushedCallController(ILogger<PushedCallController> logger, IUiService service)
        {
            _logger = logger;
            _service = service;
        }

        public async Task<IActionResult> Details(int pushedCallId)
        {
            return View(await _service.GetPushedCallDetails(pushedCallId));
        }
    }
}