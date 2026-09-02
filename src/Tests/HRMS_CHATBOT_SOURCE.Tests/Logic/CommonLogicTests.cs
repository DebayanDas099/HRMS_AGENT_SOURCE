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
}
