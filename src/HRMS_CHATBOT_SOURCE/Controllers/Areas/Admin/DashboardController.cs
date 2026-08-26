using HRMS_CHATBOT_SOURCE.Domain.Constants;
using HRMS_CHATBOT_SOURCE.Domain.Dto.ViewModel;
using HRMS_CHATBOT_SOURCE.Infrastructure.Middleware;
using HRMS_CHATBOT_SOURCE.Infrastructure.Security;
using HRMS_CHATBOT_SOURCE.Logic;
using Microsoft.AspNetCore.Mvc;

namespace HRMS_CHATBOT_SOURCE.Controllers.Areas.Admin;

[Area("Admin")]
[AdminAuthorize]
public class DashboardController : Controller
{
    private readonly IDocumentLogic _documentLogic;

    public DashboardController(IDocumentLogic documentLogic)
    {
        _documentLogic = documentLogic;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var statistics = await TryGetStatisticsAsync(cancellationToken);

        return View(new DashboardViewModel
        {
            UserName = AdminAuthHelper.FindClaim(User, HttpContext, "UserName")
                ?? User.Identity?.Name
                ?? "Admin",
            Department = AdminAuthHelper.FindClaim(User, HttpContext, AdminClaimTypes.Department) ?? "—",
            Designation = AdminAuthHelper.FindClaim(User, HttpContext, AdminClaimTypes.Designation) ?? "Administrator",
            GroupCode = AdminAuthHelper.FindClaim(User, HttpContext, AdminClaimTypes.GroupCode, "UserGroup") ?? "—",
            TotalDocuments = statistics?.TotalDocuments ?? 0,
            PolicyDocuments = statistics?.PolicyDocuments ?? 0,
            TrainingDocuments = statistics?.TrainingDocuments ?? 0,
            ActiveDocuments = statistics?.ActiveDocuments ?? 0
        });
    }

    private async Task<Domain.Dto.Response.DocumentStatisticsDto?> TryGetStatisticsAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _documentLogic.GetStatisticsAsync(cancellationToken);
        }
        catch
        {
            return null;
        }
    }
}
