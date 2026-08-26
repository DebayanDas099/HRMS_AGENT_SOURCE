using HRMS_CHATBOT_SOURCE.Agent.Configuration;
using HRMS_CHATBOT_SOURCE.Domain.Constants;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS_CHATBOT_SOURCE.Agent;

/// <summary>
/// Registers agent-framework scaffolding. Concrete agents and handoff workflow
/// execution will be added in a later phase.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddHrmsAgentFramework(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AgentFoundrySettings>(configuration.GetSection(AgentFoundrySettings.SectionName));
        services.AddSingleton<HandoffWorkflowTemplate>();

        return services;
    }
}
