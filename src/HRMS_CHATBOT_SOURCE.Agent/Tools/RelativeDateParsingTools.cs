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
        + "'this week', 'September 2026', 'current and last month') into one or more "
        + "concrete start and end dates (yyyy-MM-dd). Use the injected Current Date as the anchor. "
        + "If range_count is greater than 1, call leave balance once per item in ranges; do not merge.")]
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
            message = result.Message,
            range_count = result.Ranges.Count,
            ranges = result.Ranges.Select(r => new
            {
                start_date = r.StartDate,
                end_date = r.EndDate,
                type = r.Type,
                label = r.Label
            })
        });
    }
}
