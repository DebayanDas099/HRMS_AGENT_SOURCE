using HRMS_CHATBOT_SOURCE.Domain.Constants;
using Lucene.Net.Analysis;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Util;

namespace HRMS_CHATBOT_SOURCE.RAG.Helpers;

internal static class LuceneQueryBuilder
{
    private static readonly string[] SearchFields =
    [
        VectorPayloadFields.Content,
        VectorPayloadFields.Title,
        VectorPayloadFields.SectionPath
    ];

    /// <summary>
    /// Builds the scoring query. User text is escaped first: an unescaped operator,
    /// tilde or unbalanced quote from a natural-language question either throws
    /// ParseException or silently changes the query's meaning.
    /// </summary>
    internal static Query BuildQuery(
        string rawQuery,
        Analyzer analyzer,
        float titleBoost,
        float sectionPathBoost)
    {
        var boosts = new Dictionary<string, float>(StringComparer.Ordinal)
        {
            [VectorPayloadFields.Content] = RetrievalDefaults.ContentBoost,
            [VectorPayloadFields.Title] = titleBoost,
            [VectorPayloadFields.SectionPath] = sectionPathBoost
        };

        var parser = new MultiFieldQueryParser(LuceneVersion.LUCENE_48, SearchFields, analyzer, boosts)
        {
            DefaultOperator = Operator.OR
        };

        return parser.Parse(QueryParserBase.Escape(rawQuery));
    }

    /// <summary>
    /// Category and active-state filters.
    /// <para>
    /// Wrapped in a <see cref="QueryWrapperFilter"/> and passed to the
    /// Search(Query, Filter, int) overload rather than added as Occur.MUST clauses:
    /// Lucene.NET 4.8 has no Occur.FILTER (it arrived in Lucene 5.1), so MUST clauses
    /// would contribute their own term scores and corrupt the BM25 ranking. A Filter
    /// only contributes its doc set.
    /// </para>
    /// </summary>
    internal static Filter? BuildFilter(string? category, bool activeOnly)
    {
        var clauses = new BooleanQuery();
        var hasClause = false;

        if (!string.IsNullOrWhiteSpace(category))
        {
            clauses.Add(
                new TermQuery(new Term(VectorPayloadFields.Category, category.Trim())),
                Occur.MUST);
            hasClause = true;
        }

        if (activeOnly)
        {
            clauses.Add(
                new TermQuery(new Term(VectorPayloadFields.IsActive, bool.TrueString.ToLowerInvariant())),
                Occur.MUST);
            hasClause = true;
        }

        return hasClause ? new QueryWrapperFilter(clauses) : null;
    }
}
