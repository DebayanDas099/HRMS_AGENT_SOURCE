using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;

public class StorageSettings
{
    public const string SectionName = "Storage";

    [JsonProperty("connection_string")]
    public string? ConnectionString { get; set; }

    [JsonProperty("account_key")]
    public string? AccountKey { get; set; }

    [JsonProperty("blob_container")]
    public string BlobContainer { get; set; } = "hrms-documents";

    [JsonProperty("table_name")]
    public string TableName { get; set; } = "HrmsChatbot";
}
