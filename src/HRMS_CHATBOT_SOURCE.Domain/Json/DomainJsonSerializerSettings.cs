using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace HRMS_CHATBOT_SOURCE.Domain.Json;

public static class DomainJsonSerializerSettings
{
    public static JsonSerializerSettings Default { get; } = Create();

    public static JsonSerializerSettings Create(bool ignoreNullValues = true)
    {
        return new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            },
            NullValueHandling = ignoreNullValues ? NullValueHandling.Ignore : NullValueHandling.Include,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };
    }
}
