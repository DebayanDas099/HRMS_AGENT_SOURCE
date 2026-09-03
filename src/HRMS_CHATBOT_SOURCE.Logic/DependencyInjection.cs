using HRMS_CHATBOT_SOURCE.Logic.Common;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS_CHATBOT_SOURCE.Logic;

public static class DependencyInjection
{
    public static IServiceCollection AddLogic(this IServiceCollection services)
    {
        services.AddScoped<ICommonLogic, CommonLogic>();
        services.AddScoped<IAdminLogic, AdminLogic>();
        services.AddScoped<IDocumentLogic, DocumentLogic>();
        services.AddScoped<IAgentLogic, AgentLogic>();
        services.AddScoped<IAgentAccessService, AgentAccessService>();
        services.AddScoped<IChatLogic, ChatLogic>();
        services.AddScoped<ILeaveLogic, LeaveLogic>();
        services.AddScoped<ILeaveApprovalLogic, LeaveApprovalLogic>();
        return services;
    }
}
