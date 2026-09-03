using System.ComponentModel.DataAnnotations;
using System.Data;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Domain.Models;

namespace HRMS_CHATBOT_SOURCE.Logic.Adapter;

internal static class LeaveAdapter
{
    internal static IReadOnlyList<LeaveBalanceCategoryDto> MapBalanceCategories(MSSQLResponse? response)
    {
        EnsureSuccess(response);

        if (response?.Data is not DataSet { Tables.Count: > 0 } dataSet
            || dataSet.Tables[0].Rows.Count == 0)
        {
            return [];
        }

        var list = new List<LeaveBalanceCategoryDto>();
        foreach (DataRow row in dataSet.Tables[0].Rows)
        {
            list.Add(new LeaveBalanceCategoryDto
            {
                EmpId = ReadOptionalString(row, "emp_id") ?? string.Empty,
                LeaveCategory = ReadOptionalString(row, "leave_category") ?? string.Empty,
                CategoryValue = ToDecimal(row["category_value"]),
                LossOfPay = ToDecimal(row["loss_of_pay"]),
                ContractEndDate = ReadOptionalDateTime(row, "contract_end_date"),
                DaysUntilContractEnd = ReadOptionalInt(row, "days_until_contract_end"),
                ContractStatus = ReadOptionalString(row, "contract_status")
            });
        }

        return list;
    }

    internal static List<PendingLeaveApplicationDto> MapPendingApplications(MSSQLResponse? response)
    {
        EnsureSuccess(response);

        if (response?.Data is not DataSet { Tables.Count: > 0 } dataSet)
        {
            return [];
        }

        var rows = new List<PendingLeaveApplicationDto>();
        foreach (DataRow row in dataSet.Tables[0].Rows)
        {
            rows.Add(new PendingLeaveApplicationDto
            {
                ApplicationReference = ReadOptionalString(row, "application_reference") ?? string.Empty,
                Mobile = ReadOptionalString(row, "mobile") ?? string.Empty,
                EmployeeName = ReadOptionalString(row, "employee_name"),
                FromDate = ReadOptionalDateTime(row, "from_date") ?? default,
                ToDate = ReadOptionalDateTime(row, "to_date") ?? default,
                Reason = ReadOptionalString(row, "reason"),
                AppliedOn = ReadOptionalDateTime(row, "applied_on") ?? default
            });
        }

        return rows;
    }

    internal static string MapApplyResult(MSSQLResponse? response)
    {
        EnsureSuccess(response);

        var outputMsg = Convert.ToString(response?.OutputParameters?.FirstOrDefault(p =>
            string.Equals(p.ParameterName, "@outputMsg", StringComparison.OrdinalIgnoreCase))?.Value);

        return string.IsNullOrWhiteSpace(outputMsg)
            ? "Leave application submitted successfully."
            : outputMsg;
    }

    internal static IReadOnlyList<HolidayListItemDto> MapHolidayList(MSSQLResponse? response)
    {
        EnsureSuccess(response);

        if (response?.Data is not DataSet { Tables.Count: > 0 } dataSet)
        {
            return [];
        }

        var list = new List<HolidayListItemDto>();
        foreach (DataRow row in dataSet.Tables[0].Rows)
        {
            list.Add(new HolidayListItemDto
            {
                HolidayDate = Convert.ToDateTime(row["holiday_date"]).Date,
                HolidayName = ReadOptionalString(row, "holiday_name") ?? string.Empty,
                HolidayType = ReadOptionalString(row, "holiday_type")
            });
        }

        return list;
    }

    internal static void EnsureSuccess(MSSQLResponse? response)
    {
        var outputCode = int.TryParse(Convert.ToString(response?.OutputParameters?.FirstOrDefault(p =>
            string.Equals(p.ParameterName, "@outputCode", StringComparison.OrdinalIgnoreCase))?.Value), out var code)
            ? code
            : -1;

        var outputMsg = Convert.ToString(response?.OutputParameters?.FirstOrDefault(p =>
            string.Equals(p.ParameterName, "@outputMsg", StringComparison.OrdinalIgnoreCase))?.Value);

        if (outputCode != 1)
        {
            throw new ValidationException(string.IsNullOrWhiteSpace(outputMsg) ? "Leave operation failed." : outputMsg);
        }
    }

    private static decimal ToDecimal(object? value)
    {
        return value == null || value == DBNull.Value ? 0m : Convert.ToDecimal(value);
    }

    private static string? ReadOptionalString(DataRow row, string columnName)
    {
        return row.Table.Columns.Contains(columnName)
            ? Convert.ToString(row[columnName])
            : null;
    }

    private static DateTime? ReadOptionalDateTime(DataRow row, string columnName)
    {
        if (!row.Table.Columns.Contains(columnName))
        {
            return null;
        }

        var value = row[columnName];
        return value == null || value == DBNull.Value ? null : Convert.ToDateTime(value);
    }

    private static int? ReadOptionalInt(DataRow row, string columnName)
    {
        if (!row.Table.Columns.Contains(columnName))
        {
            return null;
        }

        var value = row[columnName];
        return value == null || value == DBNull.Value ? null : Convert.ToInt32(value);
    }

    private static long? ReadOptionalLong(DataRow row, string columnName)
    {
        if (!row.Table.Columns.Contains(columnName))
        {
            return null;
        }

        var value = row[columnName];
        return value == null || value == DBNull.Value ? null : Convert.ToInt64(value);
    }
}
