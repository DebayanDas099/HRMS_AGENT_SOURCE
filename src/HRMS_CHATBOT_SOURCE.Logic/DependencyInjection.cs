using Microsoft.Extensions.DependencyInjection;

namespace HRMS_CHATBOT_SOURCE.Logic;

public static class DependencyInjection
{
    public static IServiceCollection AddLogic(this IServiceCollection services)
    {
        services.AddScoped<IAdminLogic, AdminLogic>();
        services.AddScoped<IDocumentLogic, DocumentLogic>();
        services.AddScoped<IAgentLogic, AgentLogic>();
        services.AddScoped<IAgentAccessService, AgentAccessService>();
        return services;
    }
}
