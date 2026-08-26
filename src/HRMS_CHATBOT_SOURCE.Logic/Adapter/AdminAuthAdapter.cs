using System.ComponentModel.DataAnnotations;
using System.Data;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Domain.Models;

namespace HRMS_CHATBOT_SOURCE.Logic.Adapter;

internal static class AdminAuthAdapter
{
    internal static AuthenticatedAdminUser MapValidateAdminLoginResponse(MSSQLResponse? data)
    {
        if (data == null)
        {
            throw new ValidationException("Identity domain response is null or empty.");
        }

        var outputCode = int.TryParse(Convert.ToString(data.OutputParameters?.FirstOrDefault(p =>
            string.Equals(p.ParameterName, "@outputCode", StringComparison.OrdinalIgnoreCase))?.Value), out var code)
            ? code
            : -1;

        var outputMsg = Convert.ToString(data.OutputParameters?.FirstOrDefault(p =>
            string.Equals(p.ParameterName, "@outputMsg", StringComparison.OrdinalIgnoreCase))?.Value);

        if (outputCode != 1)
        {
            throw new ValidationException(
                string.IsNullOrWhiteSpace(outputMsg) ? "Invalid user ID or password." : outputMsg);
        }

        if (data.Data is not DataSet { Tables.Count: > 0 } dataSet
            || dataSet.Tables[0].Rows.Count == 0)
        {
            throw new ValidationException("Invalid user ID or password.");
        }

        var row = dataSet.Tables[0].Rows[0];

        return new AuthenticatedAdminUser
        {
            UserId = Convert.ToString(row["user_id"]) ?? string.Empty,
            FirstName = Convert.ToString(row["first_name"]) ?? string.Empty,
            LastName = Convert.ToString(row["last_name"]),
            FullName = Convert.ToString(row["full_name"]) ?? string.Empty,
            GroupCode = Convert.ToString(row["group_code"]) ?? string.Empty,
            Department = Convert.ToString(row["depot_name"]) ?? Convert.ToString(row["department"]) ?? string.Empty,
            Designation = Convert.ToString(row["designation"]),
            EmployeeId = Convert.ToString(row["employee_id"]),
            Email = Convert.ToString(row["mail_id"]),
            Active = Convert.ToString(row["active"]) ?? "N",
            AdminYn = row.Table.Columns.Contains("usp_admin_yn")
                ? Convert.ToString(row["usp_admin_yn"])
                : Convert.ToString(row["admin_yn"]),
            ExitDate = row.Table.Columns.Contains("last_working_date") && !row.IsNull("last_working_date")
                ? Convert.ToDateTime(row["last_working_date"])
                : null
        };
    }
}
