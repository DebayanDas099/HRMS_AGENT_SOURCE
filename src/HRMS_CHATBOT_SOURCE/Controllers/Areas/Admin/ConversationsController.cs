using HRMS_CHATBOT_SOURCE.Infrastructure.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace HRMS_CHATBOT_SOURCE.Controllers.Areas.Admin;

[Area("Admin")]
[AdminAuthorize]
public class ConversationsController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
