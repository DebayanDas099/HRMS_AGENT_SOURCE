using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.RAG.Helpers;

/// <summary>
/// Parses the reranker's JSON scores. Models wrap JSON in prose or markdown fences
/// often enough that tolerating it is cheaper than retrying the call.
/// </summary>
internal static class RerankResponseParser
{
    internal static Dictionary<int, double> Parse(string? content)
    {
        var scores = new Dictionary<int, double>();

        if (string.IsNullOrWhiteSpace(content))
        {
            return scores;
        }

        var json = ExtractJsonObject(content);
        if (json == null)
        {
            return scores;
        }

        try
        {
            var payload = JsonConvert.DeserializeObject<RerankPayload>(json);

            if (payload?.Scores == null)
            {
                return scores;
            }

            foreach (var score in payload.Scores.Where(score => score.Id >= 0))
            {
                scores[score.Id] = score.Relevance;
            }
        }
        catch (JsonException)
        {
            return [];
        }

        return scores;
    }

    private static string? ExtractJsonObject(string content)
    {
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');

        return start >= 0 && end > start
            ? content[start..(end + 1)]
            : null;
    }

    private sealed class RerankPayload
    {
        [JsonProperty("scores")]
        public List<RerankScore>? Scores { get; set; }
    }

    private sealed class RerankScore
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("relevance")]
        public double Relevance { get; set; }
    }
}
