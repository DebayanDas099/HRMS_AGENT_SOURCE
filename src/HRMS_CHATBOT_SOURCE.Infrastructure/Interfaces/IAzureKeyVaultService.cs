using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

namespace HRMS_CHATBOT_SOURCE.Infrastructure.Interfaces;

public interface IAzureKeyVaultService
{
    ApplicationSecrets Secrets { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
