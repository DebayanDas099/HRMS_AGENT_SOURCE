using HRMS_CHATBOT_SOURCE.Logic.Common;

namespace HRMS_CHATBOT_SOURCE.Tests.Logic;

public class CommonLogicTests
{
    [Fact]
    public void ParseRelativeDate_LastMonth_WithSeptemberReference_ReturnsAugustRange()
    {
        var logic = new CommonLogic();
        var reference = new DateTime(2026, 9, 1);

        var result = logic.ParseRelativeDate("last month", reference);

        Assert.True(result.Found);
        var range = Assert.Single(result.Ranges);
        Assert.Equal("2026-08-01", range.StartDate);
        Assert.Equal("2026-08-31", range.EndDate);
        Assert.Equal("August 2026", range.Label);
    }

    [Fact]
    public void ParseRelativeDate_EmptyPhrase_ReturnsNotFound()
    {
        var logic = new CommonLogic();

        var result = logic.ParseRelativeDate(string.Empty);

        Assert.False(result.Found);
        Assert.Empty(result.Ranges);
        Assert.Contains("No date phrase", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseRelativeDateFromUserMessage_ScansFullMessage()
    {
        var logic = new CommonLogic();
        var reference = new DateTime(2026, 9, 1);

        var result = logic.ParseRelativeDateFromUserMessage(
            "show my leave balance for last month",
            reference);

        Assert.True(result.Found);
        var range = Assert.Single(result.Ranges);
        Assert.Equal("2026-08-01", range.StartDate);
        Assert.Equal("2026-08-31", range.EndDate);
    }

    [Fact]
    public void ParseRelativeDate_CurrentAndLastMonth_ReturnsAugustAndSeptember()
    {
        var logic = new CommonLogic();
        var reference = new DateTime(2026, 9, 2);

        var result = logic.ParseRelativeDate("current and last month", reference);

        Assert.True(result.Found);
        Assert.Equal(2, result.Ranges.Count);
        Assert.Equal("2026-08-01", result.Ranges[0].StartDate);
        Assert.Equal("2026-08-31", result.Ranges[0].EndDate);
        Assert.Equal("2026-09-01", result.Ranges[1].StartDate);
        Assert.Equal("2026-09-30", result.Ranges[1].EndDate);
        Assert.Contains("once per range", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseRelativeDateFromUserMessage_CurrentAndLastMonthQuestion_ReturnsTwoMonthRanges()
    {
        var logic = new CommonLogic();
        var reference = new DateTime(2026, 9, 2);

        var result = logic.ParseRelativeDateFromUserMessage(
            "how many leaves of current and last month do i have ?",
            reference);

        Assert.True(result.Found);
        Assert.Equal(2, result.Ranges.Count);
        Assert.Equal("2026-08-01", result.Ranges[0].StartDate);
        Assert.Equal("2026-08-31", result.Ranges[0].EndDate);
        Assert.Equal("2026-09-01", result.Ranges[1].StartDate);
        Assert.Equal("2026-09-30", result.Ranges[1].EndDate);
    }

    [Fact]
    public void GetReferenceDateTime_ConvertsUtcToIst()
    {
        var logic = new CommonLogic();
        var utc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        var ist = logic.GetReferenceDateTime(utc);

        Assert.Equal(new DateTime(2026, 9, 1, 5, 30, 0), ist);
    }

    [Fact]
    public void FormatAgentReply_MultiDocumentDisambiguation_BuildsStructuredOutput()
    {
        var logic = new CommonLogic();
        var rawReply =
            "Your request for the \"ai project doc\" matches multiple documents: "
            + "1. Title: AI Project Data - Document ID: 14 - Category: Policy - Active: Yes - Ingestion Status: Completed - Similarity Score: 100 "
            + "2. Title: AI Project Data - Document ID: 1 - Category: Policy - Active: Yes - Ingestion Status: Completed - Similarity Score: 100 "
            + "Could you please confirm the document ID (1 or 14) you wish to download?";

        var formatted = logic.FormatAgentReply(rawReply);

        Assert.Contains("🔎 **Multiple Documents Found**", formatted);
        Assert.Contains("**Option 1**", formatted);
        Assert.Contains("**Document ID:** 14", formatted);
        Assert.Contains("**Option 2**", formatted);
        Assert.Contains("**Document ID:** 1", formatted);
        Assert.Contains("**[ Document 14 ]**", formatted);
        Assert.Contains("**[ Document 1 ]**", formatted);
    }

    [Fact]
    public void FormatAgentReply_NormalReply_AddsReadableSpacingAndLists()
    {
        var logic = new CommonLogic();
        var rawReply =
            "Here are document details: Document ID: 10 - Category: Policy - Active: Yes - Ingestion Status: Completed Please confirm if you want me to email this link.";

        var formatted = logic.FormatAgentReply(rawReply);

        Assert.Contains("Here are document details:", formatted);
        Assert.Contains("- Document ID: 10", formatted);
        Assert.Contains("Please confirm", formatted);
        Assert.Contains("\n\nPlease confirm", formatted);
    }
}
