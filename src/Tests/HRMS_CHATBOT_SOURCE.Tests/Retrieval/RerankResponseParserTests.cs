using HRMS_CHATBOT_SOURCE.RAG.Helpers;

namespace HRMS_CHATBOT_SOURCE.Tests.Retrieval;

public class RerankResponseParserTests
{
    [Fact]
    public void Parse_WellFormedResponse_ReturnsScores()
    {
        var scores = RerankResponseParser.Parse("{\"scores\":[{\"id\":0,\"relevance\":9},{\"id\":1,\"relevance\":2.5}]}");

        Assert.Equal(2, scores.Count);
        Assert.Equal(9, scores[0]);
        Assert.Equal(2.5, scores[1]);
    }

    [Fact]
    public void Parse_JsonWrappedInProseOrCodeFence_StillParses()
    {
        var content = "Here are the rankings:\n```json\n{\"scores\":[{\"id\":0,\"relevance\":8}]}\n```\nHope that helps.";

        var scores = RerankResponseParser.Parse(content);

        Assert.Single(scores);
        Assert.Equal(8, scores[0]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("I cannot rank these passages.")]
    [InlineData("{\"scores\":[{\"id\":0,\"relevance\":")]
    [InlineData("{\"unexpected\":true}")]
    public void Parse_UnusableResponse_ReturnsEmptySoCallerKeepsFusionOrder(string? content)
    {
        Assert.Empty(RerankResponseParser.Parse(content));
    }

    [Fact]
    public void Parse_NegativeIdsAreDiscarded()
    {
        var scores = RerankResponseParser.Parse("{\"scores\":[{\"id\":-1,\"relevance\":9},{\"id\":2,\"relevance\":4}]}");

        Assert.Single(scores);
        Assert.True(scores.ContainsKey(2));
    }
}
