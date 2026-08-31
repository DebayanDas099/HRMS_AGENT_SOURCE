using HRMS_CHATBOT_SOURCE.Agent.Tools;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using HRMS_CHATBOT_SOURCE.Domain.Interfaces;
using MCC.Foundation.Guardrails.Configuration;
using MCC.Foundation.Guardrails.Extensions;
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

        // Recommended = SlangProfanity | Hate | Violence | SelfHarm | Sexual | PromptInjection | Pii,
        // all served locally (pattern catalogs + ML.NET/ONNX) with no Azure account and no new
        // Key Vault secret. FailOpen is set explicitly rather than trusting the package default:
        // a check that cannot run must block, not silently let content through.
        services.AddMccGuardrails(GuardrailCategory.Recommended, options =>
        {
            options.FailOpen = false;
        });

        services.AddSingleton<IChatClient>(sp => FoundryChatClientFactory.Create(
            sp,
            sp.GetRequiredService<IOptions<AgentFoundrySettings>>(),
            sp.GetRequiredService<IConfiguration>(),
            sp.GetRequiredService<ILoggerFactory>().CreateLogger("FoundryChatClient")));
        services.AddSingleton<PolicyKnowledgeTools>();
        services.AddSingleton<HrmsHandoffWorkflowFactory>();
        services.AddSingleton<IHrmsChatRuntime, HrmsChatRuntime>();

        return services;
    }
}
