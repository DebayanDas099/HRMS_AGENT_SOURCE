using HRMS_CHATBOT_SOURCE.Agent.Notifications;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Request;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Infrastructure.Middleware;
using HRMS_CHATBOT_SOURCE.Infrastructure.Security;
using HRMS_CHATBOT_SOURCE.Logic;
using Microsoft.AspNetCore.Mvc;

namespace HRMS_CHATBOT_SOURCE.Controllers.Areas.Admin;

[Area("Admin")]
[AdminAuthorize]
public class ApprovalsController : Controller
{
    private readonly ILeaveApprovalLogic _leaveApprovalLogic;
    private readonly IAdminNotificationStore _adminNotificationStore;

    public ApprovalsController(ILeaveApprovalLogic leaveApprovalLogic, IAdminNotificationStore adminNotificationStore)
    {
        _leaveApprovalLogic = leaveApprovalLogic;
        _adminNotificationStore = adminNotificationStore;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        // Opening the grid is what "reads" the admin notifications - the bell badge
        // clears the moment the admin actually looks at what triggered it.
        await _adminNotificationStore.MarkAllReadAsync(cancellationToken);
        return View();
    }

    [HttpGet]
    [Produces("application/json")]
    public async Task<List<PendingLeaveApplicationDto>> GetPending(CancellationToken cancellationToken)
    {
        return await _leaveApprovalLogic.GetPendingApplicationsAsync(cancellationToken);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<List<string>> Submit(
        [FromBody] LeaveApprovalSubmitRequest request,
        CancellationToken cancellationToken)
    {
        var approvedBy = AdminAuthHelper.FindClaim(User, HttpContext, "UserId", "UserName")
            ?? User.Identity?.Name
            ?? "admin";

        return await _leaveApprovalLogic.SubmitDecisionsAsync(request.Decisions, approvedBy, cancellationToken);
    }

    [HttpGet]
    [Produces("application/json")]
    public async Task<int> PendingCount(CancellationToken cancellationToken)
    {
        return await _adminNotificationStore.GetUnreadCountAsync(cancellationToken);
    }
}
