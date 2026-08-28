using System.Data;
using HRMS_CHATBOT_SOURCE.Domain.Models;
using HRMS_CHATBOT_SOURCE.Foundation.Common;
using HRMS_CHATBOT_SOURCE.Infrastructure.Core;
using MCC.Foundation.MSSQLHelper.Helper;
using MCC.Foundation.MSSQLHelper.Models;
using Microsoft.Data.SqlClient;
using SqlCommon = HRMS_CHATBOT_SOURCE.Domain.Constants.Common;

namespace HRMS_CHATBOT_SOURCE.Repo.Agent;

public class AgentRepo : IAgentRepo
{
    private readonly ISqlHelper _sqlHelper;
    private readonly IServiceContext _serviceContext;

    public AgentRepo(ISqlHelper sqlHelper, IServiceContext serviceContext)
    {
        _sqlHelper = sqlHelper;
        _serviceContext = serviceContext;
    }

    public async Task<MSSQLResponse?> GetUserGroupsAsync(CancellationToken cancellationToken = default)
    {
        var sqlParams = CreateOutputParams();

        return new MSSQLResponse
        {
            Data = await _sqlHelper.FetchData(new ExecuteDataSetRequest
            {
                CommandText = "[dbo].[Get_User_Group_List]",
                CommandTimeout = SqlCommon.SQLCommandTimeOut,
                CommandType = CommandType.StoredProcedure,
                ConnectionProperties = _serviceContext.SQLConnectionModel,
                IsMultipleTables = true,
                Parameters = sqlParams
            }),
            OutputParameters = sqlParams.Where(p => p.Direction == ParameterDirection.Output).ToArray()
        };
    }

    public async Task<MSSQLResponse?> GetMasterListAsync(CancellationToken cancellationToken = default)
    {
        var sqlParams = CreateOutputParams();

        return new MSSQLResponse
        {
            Data = await _sqlHelper.FetchData(new ExecuteDataSetRequest
            {
                CommandText = "[dbo].[Get_Agent_Master_List]",
                CommandTimeout = SqlCommon.SQLCommandTimeOut,
                CommandType = CommandType.StoredProcedure,
                ConnectionProperties = _serviceContext.SQLConnectionModel,
                IsMultipleTables = true,
                Parameters = sqlParams
            }),
            OutputParameters = sqlParams.Where(p => p.Direction == ParameterDirection.Output).ToArray()
        };
    }

    public async Task<MSSQLResponse?> UpdateMasterActiveAsync(long agentId, string active, CancellationToken cancellationToken = default)
    {
        var sqlParams = new List<SqlParameter>
        {
            new()
            {
                ParameterName = "@agent_id",
                DbType = DbType.Int64,
                Direction = ParameterDirection.Input,
                Value = agentId
            },
            new()
            {
                ParameterName = "@am_active",
                DbType = DbType.StringFixedLength,
                Direction = ParameterDirection.Input,
                Size = 1,
                Value = active
            }
        };

        sqlParams.AddRange(CreateOutputParams());

        var rowsAffected = await _sqlHelper.ExecuteNonQuery(new ExecuteNonQueryRequest
        {
            CommandText = "[dbo].[Update_Agent_Master_Active]",
            CommandTimeout = SqlCommon.SQLCommandTimeOut,
            CommandType = CommandType.StoredProcedure,
            ConnectionProperties = _serviceContext.SQLConnectionModel,
            Parameters = sqlParams.ToArray()
        });

        return new MSSQLResponse
        {
            RowsAffected = rowsAffected,
            OutputParameters = sqlParams.Where(p => p.Direction == ParameterDirection.Output).ToArray()
        };
    }

    public async Task<MSSQLResponse?> GetControlPanelAsync(
        string? userGrpCode,
        string? payroll = null,
        CancellationToken cancellationToken = default)
    {
        var sqlParams = new List<SqlParameter>
        {
            new()
            {
                ParameterName = "@user_grp_code",
                DbType = DbType.String,
                Direction = ParameterDirection.Input,
                Size = 20,
                Value = Utils.IIFStringOrDBNull(userGrpCode)
            },
            new()
            {
                ParameterName = "@user_payroll",
                DbType = DbType.String,
                Direction = ParameterDirection.Input,
                Size = 20,
                Value = Utils.IIFStringOrDBNull(payroll)
            }
        };

        sqlParams.AddRange(CreateOutputParams());

        return new MSSQLResponse
        {
            Data = await _sqlHelper.FetchData(new ExecuteDataSetRequest
            {
                CommandText = "[dbo].[Get_Agent_Control_Panel]",
                CommandTimeout = SqlCommon.SQLCommandTimeOut,
                CommandType = CommandType.StoredProcedure,
                ConnectionProperties = _serviceContext.SQLConnectionModel,
                IsMultipleTables = true,
                Parameters = sqlParams.ToArray()
            }),
            OutputParameters = sqlParams.Where(p => p.Direction == ParameterDirection.Output).ToArray()
        };
    }

    public async Task<MSSQLResponse?> GetPayrollMatrixAsync(long agentId, CancellationToken cancellationToken = default)
    {
        var sqlParams = new List<SqlParameter>
        {
            new()
            {
                ParameterName = "@agent_id",
                DbType = DbType.Int64,
                Direction = ParameterDirection.Input,
                Value = agentId
            }
        };

        sqlParams.AddRange(CreateOutputParams());

        return new MSSQLResponse
        {
            Data = await _sqlHelper.FetchData(new ExecuteDataSetRequest
            {
                CommandText = "[dbo].[Get_Agent_Group_Payroll_Matrix]",
                CommandTimeout = SqlCommon.SQLCommandTimeOut,
                CommandType = CommandType.StoredProcedure,
                ConnectionProperties = _serviceContext.SQLConnectionModel,
                IsMultipleTables = true,
                Parameters = sqlParams.ToArray()
            }),
            OutputParameters = sqlParams.Where(p => p.Direction == ParameterDirection.Output).ToArray()
        };
    }

    public async Task<MSSQLResponse?> UpdateGroupActiveAsync(
        long agentId,
        string? userGrpCode,
        string? payroll,
        string active,
        string? createdBy,
        CancellationToken cancellationToken = default)
    {
        var sqlParams = new List<SqlParameter>
        {
            new()
            {
                ParameterName = "@agent_id",
                DbType = DbType.Int64,
                Direction = ParameterDirection.Input,
                Value = agentId
            },
            new()
            {
                ParameterName = "@user_grp_code",
                DbType = DbType.String,
                Direction = ParameterDirection.Input,
                Size = 20,
                Value = Utils.IIFStringOrDBNull(userGrpCode)
            },
            new()
            {
                ParameterName = "@aaug_active",
                DbType = DbType.StringFixedLength,
                Direction = ParameterDirection.Input,
                Size = 1,
                Value = active
            },
            new()
            {
                ParameterName = "@created_by",
                DbType = DbType.String,
                Direction = ParameterDirection.Input,
                Size = 20,
                Value = Utils.IIFStringOrDBNull(createdBy)
            },
            new()
            {
                ParameterName = "@user_payroll",
                DbType = DbType.String,
                Direction = ParameterDirection.Input,
                Size = 20,
                Value = Utils.IIFStringOrDBNull(payroll)
            }
        };

        sqlParams.AddRange(CreateOutputParams());

        var rowsAffected = await _sqlHelper.ExecuteNonQuery(new ExecuteNonQueryRequest
        {
            CommandText = "[dbo].[Update_Agent_Appl_User_Grp_Active]",
            CommandTimeout = SqlCommon.SQLCommandTimeOut,
            CommandType = CommandType.StoredProcedure,
            ConnectionProperties = _serviceContext.SQLConnectionModel,
            Parameters = sqlParams.ToArray()
        });

        return new MSSQLResponse
        {
            RowsAffected = rowsAffected,
            OutputParameters = sqlParams.Where(p => p.Direction == ParameterDirection.Output).ToArray()
        };
    }

    public async Task<MSSQLResponse?> GetEnabledAgentsByMobileAsync(
        string? mobile,
        CancellationToken cancellationToken = default)
    {
        var sqlParams = new List<SqlParameter>
        {
            new()
            {
                ParameterName = "@mobile",
                DbType = DbType.String,
                Direction = ParameterDirection.Input,
                Size = 20,
                Value = Utils.IIFStringOrDBNull(mobile)
            }
        };

        sqlParams.AddRange(CreateOutputParams());

        return new MSSQLResponse
        {
            Data = await _sqlHelper.FetchData(new ExecuteDataSetRequest
            {
                CommandText = "[dbo].[Get_Enabled_Agents_By_Mobile]",
                CommandTimeout = SqlCommon.SQLCommandTimeOut,
                CommandType = CommandType.StoredProcedure,
                ConnectionProperties = _serviceContext.SQLConnectionModel,
                IsMultipleTables = true,
                Parameters = sqlParams.ToArray()
            }),
            OutputParameters = sqlParams.Where(p => p.Direction == ParameterDirection.Output).ToArray()
        };
    }

    private static SqlParameter[] CreateOutputParams()
    {
        return
        [
            new()
            {
                ParameterName = "@outputCode",
                DbType = DbType.Int32,
                Direction = ParameterDirection.Output
            },
            new()
            {
                ParameterName = "@outputMsg",
                DbType = DbType.String,
                Direction = ParameterDirection.Output,
                Size = -1
            }
        ];
    }
}
