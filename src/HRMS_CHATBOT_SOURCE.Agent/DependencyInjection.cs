using HRMS_CHATBOT_SOURCE.Agent.Tools;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HRMS_CHATBOT_SOURCE.Agent;

public static class DependencyInjection
{
    public static IServiceCollection AddHrmsAgentFramework(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AgentFoundrySettings>(configuration.GetSection(AgentFoundrySettings.SectionName));
        services.AddSingleton<HandoffWorkflowTemplate>();
        services.AddSingleton<IChatClient>(sp => FoundryChatClientFactory.Create(
            sp.GetRequiredService<IOptions<AgentFoundrySettings>>(),
            sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<ILoggerFactory>().CreateLogger("FoundryChatClient")));
        services.AddSingleton<PolicyKnowledgeTools>();
        services.AddSingleton<HrmsHandoffWorkflowFactory>();
        services.AddSingleton<IHrmsChatRuntime, HrmsChatRuntime>();

        return services;
    }
}
