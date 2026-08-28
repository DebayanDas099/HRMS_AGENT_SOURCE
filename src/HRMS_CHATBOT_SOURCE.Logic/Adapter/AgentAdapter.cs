using System.ComponentModel.DataAnnotations;
using System.Data;
using HRMS_CHATBOT_SOURCE.Domain.Constants;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Domain.Models;

namespace HRMS_CHATBOT_SOURCE.Logic.Adapter;

internal static class AgentAdapter
{
    internal static List<UserGroupDto> MapUserGroups(MSSQLResponse? response)
    {
        EnsureSuccess(response);

        if (response?.Data is not DataSet { Tables.Count: > 0 } dataSet
            || dataSet.Tables[0].Rows.Count == 0)
        {
            return [];
        }

        return dataSet.Tables[0].Rows.Cast<DataRow>().Select(MapUserGroupRow).ToList();
    }

    internal static List<AgentMasterDto> MapMasterList(MSSQLResponse? response)
    {
        EnsureSuccess(response);

        if (response?.Data is not DataSet { Tables.Count: > 0 } dataSet
            || dataSet.Tables[0].Rows.Count == 0)
        {
            return [];
        }

        return dataSet.Tables[0].Rows.Cast<DataRow>().Select(MapMasterRow).ToList();
    }

    internal static List<AgentGroupAssignmentDto> MapAssignments(MSSQLResponse? response)
    {
        EnsureSuccess(response);

        if (response?.Data is not DataSet { Tables.Count: > 0 } dataSet
            || dataSet.Tables[0].Rows.Count == 0)
        {
            return [];
        }

        return dataSet.Tables[0].Rows.Cast<DataRow>().Select(MapAssignmentRow).ToList();
    }

    internal static List<AgentPayrollAssignmentDto> MapPayrollMatrix(MSSQLResponse? response)
    {
        EnsureSuccess(response);

        if (response?.Data is not DataSet { Tables.Count: > 0 } dataSet
            || dataSet.Tables[0].Rows.Count == 0)
        {
            return [];
        }

        return dataSet.Tables[0].Rows.Cast<DataRow>().Select(MapPayrollRow).ToList();
    }

    internal static List<EnabledAgentDto> MapEnabledAgents(MSSQLResponse? response)
    {
        EnsureSuccess(response);

        if (response?.Data is not DataSet { Tables.Count: > 0 } dataSet
            || dataSet.Tables[0].Rows.Count == 0)
        {
            return [];
        }

        return dataSet.Tables[0].Rows.Cast<DataRow>().Select(row => new EnabledAgentDto
        {
            AgentId = ToLong(row["am_id"]),
            AgentName = Convert.ToString(row["am_name"]) ?? string.Empty
        }).ToList();
    }

    internal static AgentMasterDto MapMasterRow(DataRow row)
    {
        var agentName = Convert.ToString(row["am_name"]) ?? string.Empty;
        return new AgentMasterDto
        {
            AgentId = ToLong(row["am_id"]),
            AgentName = agentName,
            Active = Convert.ToString(row["am_active"]) ?? "N",
            IsLocked = Convert.ToString(row["is_locked"]) ?? "N",
            CanToggle = Convert.ToString(row["can_toggle"]) ?? "N",
            Role = ResolveRole(agentName)
        };
    }

    internal static AgentGroupAssignmentDto MapAssignmentRow(DataRow row)
    {
        var agentName = Convert.ToString(row["am_name"]) ?? string.Empty;
        return new AgentGroupAssignmentDto
        {
            AgentId = ToLong(row["am_id"]),
            AgentName = agentName,
            MasterActive = Convert.ToString(row["am_active"]) ?? "N",
            GroupActive = Convert.ToString(row["aaug_active"]) ?? "N",
            IsLocked = Convert.ToString(row["is_locked"]) ?? "N",
            CanToggle = Convert.ToString(row["can_toggle"]) ?? "N",
            Role = ResolveRole(agentName),
            UserPayroll = ReadOptionalString(row, "user_payroll")
        };
    }

    internal static AgentPayrollAssignmentDto MapPayrollRow(DataRow row)
    {
        return new AgentPayrollAssignmentDto
        {
            AgentId = ToLong(row["am_id"]),
            AgentName = Convert.ToString(row["am_name"]) ?? string.Empty,
            MasterActive = Convert.ToString(row["am_active"]) ?? "N",
            UserGrpCode = Convert.ToString(row["grp_user_group_code"]) ?? string.Empty,
            UserGrpDesc = Convert.ToString(row["grp_user_group_desc"]) ?? string.Empty,
            UserPayroll = Convert.ToString(row["user_payroll"]) ?? "onroll",
            GroupActive = Convert.ToString(row["aaug_active"]) ?? "N",
            IsLocked = Convert.ToString(row["is_locked"]) ?? "N",
            CanToggle = Convert.ToString(row["can_toggle"]) ?? "N"
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
            throw new ValidationException(string.IsNullOrWhiteSpace(outputMsg) ? "Agent operation failed." : outputMsg);
        }
    }

    private static string ResolveRole(string agentName)
    {
        return string.Equals(agentName, AgentNames.Supervisor, StringComparison.OrdinalIgnoreCase)
            ? "Coordinator"
            : "Specialist";
    }

    private static UserGroupDto MapUserGroupRow(DataRow row)
    {
        return new UserGroupDto
        {
            GroupCode = Convert.ToString(row["grp_user_group_code"]) ?? string.Empty,
            GroupDesc = Convert.ToString(row["grp_user_group_desc"]) ?? string.Empty
        };
    }

    private static string ReadOptionalString(DataRow row, string columnName)
    {
        return row.Table.Columns.Contains(columnName)
            ? Convert.ToString(row[columnName]) ?? string.Empty
            : string.Empty;
    }

    private static long ToLong(object? value)
    {
        return value == null || value == DBNull.Value ? 0 : Convert.ToInt64(value);
    }
}
