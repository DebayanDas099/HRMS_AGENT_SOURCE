using HRMS_CHATBOT_SOURCE.Domain.Dto.Request;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Infrastructure.Middleware;
using HRMS_CHATBOT_SOURCE.Infrastructure.Security;
using HRMS_CHATBOT_SOURCE.Logic;
using Microsoft.AspNetCore.Mvc;

namespace HRMS_CHATBOT_SOURCE.Controllers.Areas.Admin;

[Area("Admin")]
[AdminAuthorize]
public class AgentControlPanelController : Controller
{
    private readonly IAgentLogic _agentLogic;

    public AgentControlPanelController(IAgentLogic agentLogic)
    {
        _agentLogic = agentLogic;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    [Produces("application/json")]
    public async Task<List<UserGroupDto>> GetGroups(CancellationToken cancellationToken)
    {
        return await _agentLogic.GetUserGroupsAsync(cancellationToken);
    }

    [HttpGet]
    [Produces("application/json")]
    public async Task<AgentMasterListResponseDto> GetModels(CancellationToken cancellationToken)
    {
        return await _agentLogic.GetMasterListAsync(cancellationToken);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<AgentMasterDto> UpdateModelActive(
        [FromBody] UpdateAgentMasterActiveRequest request,
        CancellationToken cancellationToken)
    {
        return await _agentLogic.UpdateMasterActiveAsync(request.AgentId, request.IsActive, cancellationToken);
    }

    [HttpGet]
    [Produces("application/json")]
    public async Task<AgentControlPanelResponseDto> GetAssignments(
        string user_grp_code,
        string? user_payroll,
        CancellationToken cancellationToken)
    {
        return await _agentLogic.GetControlPanelAsync(user_grp_code, user_payroll, cancellationToken);
    }

    [HttpGet]
    [Produces("application/json")]
    public async Task<AgentPayrollMatrixResponseDto> GetAssignmentsForAgent(
        long agent_id,
        CancellationToken cancellationToken)
    {
        return await _agentLogic.GetPayrollMatrixAsync(agent_id, cancellationToken);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<AgentPayrollAssignmentDto> UpdateActive(
        [FromBody] UpdateAgentGroupActiveRequest request,
        CancellationToken cancellationToken)
    {
        var createdBy = AdminAuthHelper.FindClaim(User, HttpContext, "UserId", "UserName")
            ?? User.Identity?.Name
            ?? "admin";

        return await _agentLogic.UpdateGroupActiveAsync(
            request.AgentId,
            request.UserGrpCode,
            request.UserPayroll,
            request.IsActive,
            createdBy,
            cancellationToken);
    }
}
