using HRMS_CHATBOT_SOURCE.Agent.Tools;
using HRMS_CHATBOT_SOURCE.RAG.Abstractions;
using HRMS_CHATBOT_SOURCE.RAG.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace HRMS_CHATBOT_SOURCE.Tests.Agent;

public class PolicyKnowledgeToolsTests
{
    [Fact]
    public async Task SearchPolicyDocumentsAsync_MapsPassagesWithCitationFields()
    {
        var (tools, _) = Build(new RetrievalResponse
        {
            Confidence = 0.9,
            IsConfident = true,
            Results =
            [
                new VectorSearchResult
                {
                    Title = "HR POSH Policy",
                    Category = "Policy",
                    SectionPath = "Complaints > Committee",
                    Content = "The committee handles complaints."
                }
            ]
        });

        var result = await tools.SearchPolicyDocumentsAsync("who handles POSH complaints");

        Assert.True(result.Grounded);
        var passage = Assert.Single(result.Passages);
        Assert.Equal("HR POSH Policy", passage.Title);
        Assert.Equal("Complaints > Committee", passage.Section);
        Assert.Null(result.Guidance);
    }

    [Fact]
    public async Task SearchPolicyDocumentsAsync_AlwaysRestrictsToActiveDocuments()
    {
        // The model supplies the question only. Visibility filters are server-side, so no
        // prompt injection can persuade the agent to reach a deactivated document.
        var (tools, spy) = Build(Empty());

        await tools.SearchPolicyDocumentsAsync("anything");

        Assert.True(spy.LastQuery!.ActiveOnly);
    }

    [Theory]
    [InlineData("Policy", "Policy")]
    [InlineData("training", "Training")]
    [InlineData("Payroll", null)]
    [InlineData("'; DROP TABLE--", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public async Task SearchPolicyDocumentsAsync_OnlyHonoursKnownCategories(string? supplied, string? expected)
    {
        var (tools, spy) = Build(Empty());

        await tools.SearchPolicyDocumentsAsync("question", supplied);

        Assert.Equal(expected, spy.LastQuery!.Category);
    }

    [Fact]
    public async Task SearchPolicyDocumentsAsync_NoResults_TellsModelToDecline()
    {
        var (tools, _) = Build(Empty());

        var result = await tools.SearchPolicyDocumentsAsync("what is the pet bereavement policy");

        Assert.False(result.Grounded);
        Assert.Empty(result.Passages);
        Assert.NotNull(result.Guidance);
    }

    [Fact]
    public async Task SearchPolicyDocumentsAsync_LowConfidence_ReturnsPassagesButInstructsDecline()
    {
        var (tools, _) = Build(new RetrievalResponse
        {
            Confidence = 0.2,
            IsConfident = false,
            Results = [new VectorSearchResult { Title = "Vaguely Related Policy", Content = "..." }]
        });

        var result = await tools.SearchPolicyDocumentsAsync("something obscure");

        Assert.False(result.Grounded);
        Assert.NotEmpty(result.Passages);
        Assert.Contains("could not find an approved answer", result.Guidance);
    }

    [Fact]
    public async Task SearchPolicyDocumentsAsync_RetrievalThrows_DegradesInsteadOfFailingTheTurn()
    {
        var (tools, _) = Build(null, throws: true);

        var result = await tools.SearchPolicyDocumentsAsync("how many CL do I get");

        Assert.False(result.Grounded);
        Assert.Contains("temporarily unavailable", result.Guidance);
    }

    [Fact]
    public async Task SearchPolicyDocumentsAsync_EmptyQuestion_DoesNotCallRetrieval()
    {
        var (tools, spy) = Build(Empty());

        var result = await tools.SearchPolicyDocumentsAsync("   ");

        Assert.False(result.Grounded);
        Assert.Null(spy.LastQuery);
    }

    [Fact]
    public void Tool_IsDiscoverableByTheModelWithADescription()
    {
        var (tools, _) = Build(Empty());

        var function = AIFunctionFactory.Create(tools.SearchPolicyDocumentsAsync);

        // AIFunctionFactory strips the Async suffix, so this is the name the model sees
        // and the name the agent instructions must reference.
        Assert.Equal("SearchPolicyDocuments", function.Name);
        Assert.False(string.IsNullOrWhiteSpace(function.Description));
    }

    [Fact]
    public void ToolResult_SerialisesWithTheFieldNamesTheInstructionsReference()
    {
        // skill.md tells the agent to check `grounded` and follow `guidance`. If the
        // serialised casing drifts, those instructions silently stop matching.
        var json = System.Text.Json.JsonSerializer.Serialize(
            new PolicySearchToolResult { Grounded = false, Guidance = "decline" },
            AIJsonUtilities.DefaultOptions);

        Assert.Contains("\"grounded\"", json);
        Assert.Contains("\"guidance\"", json);
        Assert.Contains("\"passages\"", json);
        Assert.Contains("\"confidence\"", json);
    }

    private static RetrievalResponse Empty() => new() { Results = [], IsConfident = false };

    private static (PolicyKnowledgeTools Tools, SpySearchService Spy) Build(
        RetrievalResponse? response,
        bool throws = false)
    {
        var spy = new SpySearchService(response, throws);
        var provider = new ServiceCollection()
            .AddScoped<IVectorSearchService>(_ => spy)
            .BuildServiceProvider();

        var tools = new PolicyKnowledgeTools(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PolicyKnowledgeTools>.Instance);

        return (tools, spy);
    }

    private sealed class SpySearchService(RetrievalResponse? response, bool throws) : IVectorSearchService
    {
        public RetrievalQuery? LastQuery { get; private set; }

        public Task<RetrievalResponse> SearchAsync(
            RetrievalQuery query, CancellationToken cancellationToken = default)
        {
            LastQuery = query;

            if (throws)
            {
                throw new InvalidOperationException("qdrant unreachable");
            }

            return Task.FromResult(response!);
        }

        public Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
            string query, int topK = 5, string? category = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<VectorSearchResult>>([]);
    }
}
