using System.ComponentModel;
using System.Text.Json;
using HRMS_CHATBOT_SOURCE.Logic.Common;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS_CHATBOT_SOURCE.Agent.Tools;

public sealed class RelativeDateParsingTools
{
    private readonly IServiceScopeFactory _scopeFactory;

    public RelativeDateParsingTools(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    [Description(
        "Parses a relative or natural-language date phrase (for example 'last month', "
        + "'this week', 'September 2026') into concrete start and end dates (yyyy-MM-dd). "
        + "Use the injected Current Date as the anchor."
        + "Fallback: parses a natural-language date phrase (e.g. 'last month') into start_date/end_date (yyyy-MM-dd) using Current Date as anchor.")]
    public string ParseRelativeDateRange(
        [Description("The date phrase from the user, e.g. 'last month' or 'from 1 Sep to 15 Sep'.")]
        string phrase)
    {
        using var scope = _scopeFactory.CreateScope();
        var commonLogic = scope.ServiceProvider.GetRequiredService<ICommonLogic>();
        var result = commonLogic.ParseRelativeDate(phrase);

        return JsonSerializer.Serialize(new
        {
            found = result.Found,
            phrase = result.Phrase,
            start_date = result.StartDate,
            end_date = result.EndDate,
            type = result.Type,
            message = result.Message
        }, new JsonSerializerOptions { WriteIndented = false });
    }
}
