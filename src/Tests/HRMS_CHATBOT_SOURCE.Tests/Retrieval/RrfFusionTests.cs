using HRMS_CHATBOT_SOURCE.RAG.Helpers;
using HRMS_CHATBOT_SOURCE.RAG.Models;

namespace HRMS_CHATBOT_SOURCE.Tests.Retrieval;

public class RrfFusionTests
{
    private const int RrfK = 60;

    [Fact]
    public void Fuse_ChunkFoundByBothEngines_ScoresAsSumOfReciprocalRanks()
    {
        var dense = new[] { Candidate("1:0", denseRank: 1) };
        var sparse = new[] { Candidate("1:0", sparseRank: 3) };

        var fused = RrfFusion.Fuse(dense, sparse, RrfK);

        var expected = (1.0 / (RrfK + 1)) + (1.0 / (RrfK + 3));

        Assert.Single(fused);
        Assert.Equal(expected, fused[0].FusedScore, 10);
        Assert.Equal(1, fused[0].DenseRank);
        Assert.Equal(3, fused[0].SparseRank);
    }

    [Fact]
    public void Fuse_AgreementBeatsASingleStrongerRank()
    {
        // The whole point of fusion: two engines agreeing at rank 2 and 2 should beat
        // one engine's rank 1 that the other never returned.
        var dense = new[] { Candidate("1:0", denseRank: 1), Candidate("2:0", denseRank: 2) };
        var sparse = new[] { Candidate("2:0", sparseRank: 2) };

        var fused = RrfFusion.Fuse(dense, sparse, RrfK);

        Assert.Equal("2:0", fused[0].DocumentKey);
        Assert.Equal("1:0", fused[1].DocumentKey);
    }

    [Fact]
    public void Fuse_LexicalOnlyChunkIsRetained()
    {
        // An exact-term match the embedding never returned is exactly the recall
        // gap hybrid retrieval exists to close, so it must survive fusion.
        var dense = new[] { Candidate("1:0", denseRank: 1) };
        var sparse = new[] { Candidate("9:4", sparseRank: 1) };

        var fused = RrfFusion.Fuse(dense, sparse, RrfK);

        Assert.Equal(2, fused.Count);
        Assert.Contains(fused, candidate => candidate.DocumentKey == "9:4");
    }

    [Fact]
    public void Fuse_MergesSparseMetadataIntoDenseHit()
    {
        var dense = new[] { Candidate("1:0", denseRank: 1) };
        var sparse = new[] { Candidate("1:0", sparseRank: 1, sectionPath: "Leave > Casual") };

        var fused = RrfFusion.Fuse(dense, sparse, RrfK);

        Assert.Equal("Leave > Casual", fused[0].SectionPath);
    }

    [Fact]
    public void Fuse_EmptyInputs_ReturnsEmpty()
    {
        Assert.Empty(RrfFusion.Fuse([], [], RrfK));
    }

    [Fact]
    public void Fuse_ResultsAreOrderedByFusedScoreDescending()
    {
        var dense = new[]
        {
            Candidate("1:0", denseRank: 1),
            Candidate("2:0", denseRank: 2),
            Candidate("3:0", denseRank: 3)
        };

        var fused = RrfFusion.Fuse(dense, [], RrfK);

        Assert.Equal(["1:0", "2:0", "3:0"], fused.Select(candidate => candidate.DocumentKey));
    }

    private static RetrievalCandidate Candidate(
        string key,
        int? denseRank = null,
        int? sparseRank = null,
        string sectionPath = "")
    {
        var parts = key.Split(':');

        return new RetrievalCandidate
        {
            DocumentKey = key,
            DocumentId = long.Parse(parts[0]),
            ChunkIndex = int.Parse(parts[1]),
            DenseRank = denseRank,
            SparseRank = sparseRank,
            SectionPath = sectionPath
        };
    }
}
