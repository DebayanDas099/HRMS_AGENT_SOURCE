using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;

public class VectorStoreSettings
{
    public const string SectionName = "VectorStore";

    [JsonProperty("provider")]
    public string Provider { get; set; } = Constants.VectorStoreProviders.Qdrant;

    [JsonProperty("collection_name")]
    public string CollectionName { get; set; } = "hrms-documents";

    [JsonProperty("vector_size")]
    public int VectorSize { get; set; } = 1536;
}
