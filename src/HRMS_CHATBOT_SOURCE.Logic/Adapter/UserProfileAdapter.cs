using System.Data;
using HRMS_CHATBOT_SOURCE.Domain.Models;

namespace HRMS_CHATBOT_SOURCE.Logic.Adapter;

internal static class UserProfileAdapter
{
    internal static List<string> MapActiveMobileNumbers(MSSQLResponse? response)
    {
        var outputCode = int.TryParse(Convert.ToString(response?.OutputParameters?.FirstOrDefault(p =>
            string.Equals(p.ParameterName, "@outputCode", StringComparison.OrdinalIgnoreCase))?.Value), out var code)
            ? code
            : -1;

        if (outputCode != 1)
        {
            return [];
        }

        if (response?.Data is not DataSet { Tables.Count: > 0 } dataSet
            || dataSet.Tables[0].Rows.Count == 0)
        {
            return [];
        }

        return dataSet.Tables[0].Rows.Cast<DataRow>()
            .Select(row => Convert.ToString(row["mobile_number"]))
            .Where(mobile => !string.IsNullOrWhiteSpace(mobile))
            .Select(mobile => mobile!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
