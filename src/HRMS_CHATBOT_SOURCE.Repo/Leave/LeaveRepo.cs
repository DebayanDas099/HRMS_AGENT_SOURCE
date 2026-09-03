using System.Data;
using HRMS_CHATBOT_SOURCE.Domain.Models;
using HRMS_CHATBOT_SOURCE.Foundation.Common;
using HRMS_CHATBOT_SOURCE.Infrastructure.Core;
using MCC.Foundation.MSSQLHelper.Helper;
using MCC.Foundation.MSSQLHelper.Models;
using Microsoft.Data.SqlClient;
using SqlCommon = HRMS_CHATBOT_SOURCE.Domain.Constants.Common;

namespace HRMS_CHATBOT_SOURCE.Repo.Leave;

public class LeaveRepo : ILeaveRepo
{
    private readonly ISqlHelper _sqlHelper;
    private readonly IServiceContext _serviceContext;

    public LeaveRepo(ISqlHelper sqlHelper, IServiceContext serviceContext)
    {
        _sqlHelper = sqlHelper;
        _serviceContext = serviceContext;
    }

    public async Task<MSSQLResponse?> GetLeaveDetailsByUserAsync(
        string? mobile,
        DateTime startDate,
        DateTime endDate,
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
            },
            new()
            {
                ParameterName = "@start_date",
                DbType = DbType.Date,
                Direction = ParameterDirection.Input,
                Value = startDate.Date
            },
            new()
            {
                ParameterName = "@end_date",
                DbType = DbType.Date,
                Direction = ParameterDirection.Input,
                Value = endDate.Date
            }
        };

        sqlParams.AddRange(CreateOutputParams());

        return new MSSQLResponse
        {
            Data = await _sqlHelper.FetchData(new ExecuteDataSetRequest
            {
                CommandText = "[dbo].[Get_Leave_Details_By_User]",
                CommandTimeout = SqlCommon.SQLCommandTimeOut,
                CommandType = CommandType.StoredProcedure,
                ConnectionProperties = _serviceContext.SQLConnectionModel,
                IsMultipleTables = true,
                Parameters = sqlParams.ToArray()
            }),
            OutputParameters = sqlParams.Where(p => p.Direction == ParameterDirection.Output).ToArray()
        };
    }

    public async Task<MSSQLResponse?> ValidateAndApplyLeaveByUserAsync(
        string? mobile,
        DateTime startDate,
        DateTime endDate,
        string? leaveType,
        string? reason,
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
            },
            new()
            {
                ParameterName = "@start_date",
                DbType = DbType.Date,
                Direction = ParameterDirection.Input,
                Value = startDate.Date
            },
            new()
            {
                ParameterName = "@end_date",
                DbType = DbType.Date,
                Direction = ParameterDirection.Input,
                Value = endDate.Date
            },
            new()
            {
                ParameterName = "@leave_type",
                DbType = DbType.String,
                Direction = ParameterDirection.Input,
                Size = 100,
                Value = Utils.IIFStringOrDBNull(leaveType)
            },
            new()
            {
                ParameterName = "@reason",
                DbType = DbType.String,
                Direction = ParameterDirection.Input,
                Size = -1,
                Value = Utils.IIFStringOrDBNull(reason)
            }
        };

        sqlParams.AddRange(CreateOutputParams());

        var rowsAffected = await _sqlHelper.ExecuteNonQuery(new ExecuteNonQueryRequest
        {
            CommandText = "[dbo].[Validate_And_Apply_Leave_By_User]",
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

    public async Task<MSSQLResponse?> GetHolidayListAsync(
        DateTime startDate,
        DateTime endDate,
        int? maxResults,
        CancellationToken cancellationToken = default)
    {
        var sqlParams = new List<SqlParameter>
        {
            new()
            {
                ParameterName = "@start_date",
                DbType = DbType.Date,
                Direction = ParameterDirection.Input,
                Value = startDate.Date
            },
            new()
            {
                ParameterName = "@end_date",
                DbType = DbType.Date,
                Direction = ParameterDirection.Input,
                Value = endDate.Date
            },
            new()
            {
                ParameterName = "@max_results",
                DbType = DbType.Int32,
                Direction = ParameterDirection.Input,
                Value = maxResults.HasValue ? maxResults.Value : DBNull.Value
            }
        };

        sqlParams.AddRange(CreateOutputParams());

        return new MSSQLResponse
        {
            Data = await _sqlHelper.FetchData(new ExecuteDataSetRequest
            {
                CommandText = "[dbo].[Get_Holiday_List]",
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
