using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;

public class AzureKeyVaultSettings
{
    public const string SectionName = "AzureKeyVault";

    [JsonProperty("vault_uri")]
    public string VaultUri { get; set; } = string.Empty;

    [JsonProperty("secrets_cache_path")]
    public string SecretsCachePath { get; set; } = "App_Data/keyvault-secrets.json";

    [JsonProperty("use_managed_identity")]
    public bool UseManagedIdentity { get; set; } = true;

    [JsonProperty("tenant_id")]
    public string? TenantId { get; set; }

    [JsonProperty("client_id")]
    public string? ClientId { get; set; }

    [JsonProperty("client_secret")]
    public string? ClientSecret { get; set; }
}
