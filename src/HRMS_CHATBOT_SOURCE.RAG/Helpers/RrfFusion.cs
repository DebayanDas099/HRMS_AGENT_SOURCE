using HRMS_CHATBOT_SOURCE.RAG.Models;

namespace HRMS_CHATBOT_SOURCE.RAG.Helpers;

/// <summary>
/// Reciprocal Rank Fusion over the dense and lexical result lists.
/// <para>
/// Fusion is done on rank rather than score because cosine similarity (bounded,
/// tightly clustered near the top) and BM25 (unbounded, corpus-dependent) share no
/// common scale, and normalising them requires distribution assumptions that break
/// whenever the corpus changes. Rank is the only thing both engines agree on.
/// </para>
/// <para>
/// Implemented here rather than delegated to MCC.Foundation.QdrantHelper: its
/// <c>QdrantRerankCandidateModel</c> carries a single <c>VectorScore</c> and has no
/// field for an externally-computed lexical rank, so its RRF derives the second
/// ranking from its own Lucene pass over the candidates it was given. That cannot
/// express two independent corpus-wide rank lists, which is the whole point here.
/// </para>
/// </summary>
internal static class RrfFusion
{
    internal static List<RetrievalCandidate> Fuse(
        IReadOnlyList<RetrievalCandidate> dense,
        IReadOnlyList<RetrievalCandidate> sparse,
        int rrfK)
    {
        var k = rrfK <= 0 ? 1 : rrfK;
        var merged = new Dictionary<string, RetrievalCandidate>(StringComparer.Ordinal);

        foreach (var candidate in dense)
        {
            merged[candidate.DocumentKey] = candidate;
        }

        foreach (var candidate in sparse)
        {
            if (merged.TryGetValue(candidate.DocumentKey, out var existing))
            {
                existing.SparseRank = candidate.SparseRank;
                existing.SparseScore = candidate.SparseScore;

                // Dense results omit section path when the payload predates it.
                if (string.IsNullOrEmpty(existing.SectionPath))
                {
                    existing.SectionPath = candidate.SectionPath;
                }

                continue;
            }

            merged[candidate.DocumentKey] = candidate;
        }

        foreach (var candidate in merged.Values)
        {
            double score = 0;

            if (candidate.DenseRank.HasValue)
            {
                score += 1.0 / (k + candidate.DenseRank.Value);
            }

            if (candidate.SparseRank.HasValue)
            {
                score += 1.0 / (k + candidate.SparseRank.Value);
            }

            candidate.FusedScore = score;
            candidate.FinalScore = score;
        }

        return merged.Values
            .OrderByDescending(candidate => candidate.FusedScore)
            .ThenBy(candidate => candidate.DenseRank ?? int.MaxValue)
            .ThenBy(candidate => candidate.SparseRank ?? int.MaxValue)
            .ToList();
    }
}
