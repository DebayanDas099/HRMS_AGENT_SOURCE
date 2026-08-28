using HRMS_CHATBOT_SOURCE.Domain.Constants;

namespace HRMS_CHATBOT_SOURCE.RAG.Models;

public class RetrievalQuery
{
    public string Query { get; set; } = string.Empty;

    public int TopK { get; set; } = RetrievalDefaults.TopK;

    public string? Category { get; set; }

    /// <summary>Restrict to documents still marked active. Defaults to true.</summary>
    public bool ActiveOnly { get; set; } = true;

    /// <summary>Per-request override; the configured value still gates it.</summary>
    public bool EnableRerank { get; set; } = true;

    /// <summary>Per-request override, used to compare hybrid against dense-only.</summary>
    public bool EnableLexical { get; set; } = true;
}
