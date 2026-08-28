using HRMS_CHATBOT_SOURCE.Domain.Constants;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using HRMS_CHATBOT_SOURCE.RAG.Models;
using HRMS_CHATBOT_SOURCE.RAG.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HRMS_CHATBOT_SOURCE.Tests.Retrieval;

public class LuceneLexicalIndexTests : IDisposable
{
    private readonly string _indexPath;
    private readonly LuceneLexicalIndex _index;

    public LuceneLexicalIndexTests()
    {
        _indexPath = Path.Combine(Path.GetTempPath(), "hrms-lucene-tests", Guid.NewGuid().ToString("N"));

        _index = new LuceneLexicalIndex(
            Options.Create(new RetrievalSettings { LuceneIndexPath = _indexPath }),
            NullLogger<LuceneLexicalIndex>.Instance);
    }

    [Fact]
    public async Task SearchAsync_FindsExactAcronymDenseSearchWouldMiss()
    {
        await SeedAsync();

        var results = await _index.SearchAsync("POSH", limit: 10, category: null, activeOnly: true);

        Assert.NotEmpty(results);
        Assert.Equal(1, results[0].DocumentId);
        Assert.Equal(1, results[0].SparseRank);
        Assert.NotNull(results[0].SparseScore);
    }

    [Fact]
    public async Task SearchAsync_StemsEnglishTerms()
    {
        await SeedAsync();

        // "terminated" must match a chunk that only contains "termination".
        var results = await _index.SearchAsync("terminated", limit: 10, category: null, activeOnly: true);

        Assert.Contains(results, candidate => candidate.DocumentId == 3);
    }

    [Fact]
    public async Task SearchAsync_HonoursCategoryFilter()
    {
        await SeedAsync();

        var results = await _index.SearchAsync("policy", limit: 10, category: "Training", activeOnly: true);

        Assert.All(results, candidate => Assert.Equal("Training", candidate.Category));
    }

    [Fact]
    public async Task SearchAsync_ExcludesInactiveChunksWhenActiveOnly()
    {
        await SeedAsync();
        await _index.SetActiveAsync(1, isActive: false);

        var active = await _index.SearchAsync("POSH", limit: 10, category: null, activeOnly: true);
        var all = await _index.SearchAsync("POSH", limit: 10, category: null, activeOnly: false);

        Assert.DoesNotContain(active, candidate => candidate.DocumentId == 1);
        Assert.Contains(all, candidate => candidate.DocumentId == 1);
    }

    [Fact]
    public async Task SetActiveAsync_PreservesStoredContent()
    {
        await SeedAsync();
        await _index.SetActiveAsync(1, isActive: false);
        await _index.SetActiveAsync(1, isActive: true);

        var results = await _index.SearchAsync("POSH", limit: 10, category: null, activeOnly: true);

        var hit = Assert.Single(results, candidate => candidate.DocumentId == 1);
        Assert.Contains("POSH", hit.Content);
        Assert.Equal("Policy", hit.Category);
        Assert.Equal("HR POSH Policy", hit.Title);
    }

    [Fact]
    public async Task DeleteDocumentAsync_RemovesEveryChunkOfThatDocument()
    {
        await SeedAsync();

        await _index.DeleteDocumentAsync(1);

        var results = await _index.SearchAsync("POSH", limit: 10, category: null, activeOnly: true);
        Assert.DoesNotContain(results, candidate => candidate.DocumentId == 1);
    }

    [Fact]
    public async Task ReplaceDocumentAsync_DoesNotDuplicateOnReingest()
    {
        await SeedAsync();
        await SeedAsync();

        Assert.Equal(4, await _index.CountAsync());
    }

    [Theory]
    [InlineData("what happens AND when")]
    [InlineData("leave~ policy^^")]
    [InlineData("\"unbalanced quote")]
    [InlineData("a || b && !c")]
    [InlineData("(((")]
    public async Task SearchAsync_HostileQuerySyntaxIsEscapedNotThrown(string query)
    {
        await SeedAsync();

        var exception = await Record.ExceptionAsync(
            () => _index.SearchAsync(query, limit: 5, category: null, activeOnly: true));

        Assert.Null(exception);
    }

    [Fact]
    public async Task SearchAsync_ScoresWithBm25NotDefaultTfIdf()
    {
        // Lucene 4.8 defaults to TF-IDF; BM25 must be set explicitly on the searcher.
        // TF-IDF grows without bound as a term repeats, whereas BM25 saturates, so a
        // chunk repeating the term 40 times must not score 40x a single occurrence.
        await _index.ReplaceAllAsync(
        [
            Document(100, 0, "gratuity", "Gratuity once."),
            Document(101, 0, "gratuity", string.Join(' ', Enumerable.Repeat("gratuity", 40)))
        ]);

        var results = await _index.SearchAsync("gratuity", limit: 10, category: null, activeOnly: true);

        Assert.Equal(2, results.Count);

        var single = results.First(candidate => candidate.DocumentId == 100).SparseScore!.Value;
        var repeated = results.First(candidate => candidate.DocumentId == 101).SparseScore!.Value;

        Assert.True(
            repeated < single * 5,
            $"Term-frequency saturation absent (single={single}, repeated={repeated}); similarity is probably still TF-IDF.");
    }

    [Fact]
    public async Task SearchAsync_EmptyQueryReturnsNothing()
    {
        await SeedAsync();

        Assert.Empty(await _index.SearchAsync("   ", limit: 5, category: null, activeOnly: true));
    }

    private Task SeedAsync()
    {
        return _index.ReplaceAllAsync(
        [
            Document(1, 0, "Policy", "The POSH committee handles workplace harassment complaints.", "HR POSH Policy"),
            Document(2, 0, "Training", "This onboarding policy module covers company values.", "Onboarding"),
            Document(3, 0, "Policy", "Notice period applies on termination of employment.", "Exit Policy"),
            Document(3, 1, "Policy", "Gratuity is payable after five years of service.", "Exit Policy")
        ]);
    }

    private static LexicalDocument Document(
        long documentId,
        int chunkIndex,
        string category,
        string content,
        string title = "Untitled")
    {
        return new LexicalDocument
        {
            DocumentId = documentId,
            ChunkIndex = chunkIndex,
            Category = category,
            Title = title,
            Content = content,
            SectionPath = string.Empty,
            IsActive = true
        };
    }

    public void Dispose()
    {
        _index.Dispose();

        try
        {
            if (Directory.Exists(_indexPath))
            {
                Directory.Delete(_indexPath, recursive: true);
            }
        }
        catch (IOException)
        {
            // Temp directory cleanup is best-effort.
        }

        GC.SuppressFinalize(this);
    }
}
