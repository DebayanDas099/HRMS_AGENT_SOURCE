using System.ComponentModel.DataAnnotations;
using System.Data;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Domain.Models;

namespace HRMS_CHATBOT_SOURCE.Logic.Adapter;

internal static class LeaveAdapter
{
    internal static LeaveBalanceSummaryDto MapBalanceSummary(MSSQLResponse? response)
    {
        EnsureSuccess(response);

        if (response?.Data is not DataSet { Tables.Count: > 0 } dataSet
            || dataSet.Tables[0].Rows.Count == 0)
        {
            return new LeaveBalanceSummaryDto();
        }

        var row = dataSet.Tables[0].Rows[0];
        return new LeaveBalanceSummaryDto
        {
            EmpId = ReadOptionalString(row, "emp_id") ?? string.Empty,
            AccruedLeaveBalance = ToDecimal(row["accrued_leave_balance"]),
            AppliedLeave = ToDecimal(row["applied_leave"]),
            RemainingLeaveBalance = ToDecimal(row["remaining_leave_balance"]),
            LossOfPay = ToDecimal(row["loss_of_pay"]),
            ContractEndDate = ReadOptionalDateTime(row, "contract_end_date"),
            DaysUntilContractEnd = ReadOptionalInt(row, "days_until_contract_end"),
            ContractStatus = ReadOptionalString(row, "contract_status"),
            LeaveTypeLdLovIdFk = ReadOptionalLong(row, "leave_type_ld_lov_id_fk") ?? 0
        };
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
