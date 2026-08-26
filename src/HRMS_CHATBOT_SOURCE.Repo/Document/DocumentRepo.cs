using System.Data;
using HRMS_CHATBOT_SOURCE.Domain.Models;
using HRMS_CHATBOT_SOURCE.Foundation.Common;
using HRMS_CHATBOT_SOURCE.Infrastructure.Core;
using MCC.Foundation.MSSQLHelper.Helper;
using MCC.Foundation.MSSQLHelper.Models;
using Microsoft.Data.SqlClient;
using SqlCommon = HRMS_CHATBOT_SOURCE.Domain.Constants.Common;

namespace HRMS_CHATBOT_SOURCE.Repo.Document;

public class DocumentRepo : IDocumentRepo
{
    private readonly ISqlHelper _sqlHelper;
    private readonly IServiceContext _serviceContext;

    public DocumentRepo(ISqlHelper sqlHelper, IServiceContext serviceContext)
    {
        _sqlHelper = sqlHelper;
        _serviceContext = serviceContext;
    }

    public async Task<MSSQLResponse?> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var sqlParams = CreateOutputParams();

        return new MSSQLResponse
        {
            Data = await _sqlHelper.FetchData(new ExecuteDataSetRequest
            {
                CommandText = "[dbo].[Get_Document_Mstr_Statistics]",
                CommandTimeout = SqlCommon.SQLCommandTimeOut,
                CommandType = CommandType.StoredProcedure,
                ConnectionProperties = _serviceContext.SQLConnectionModel,
                IsMultipleTables = true,
                Parameters = sqlParams
            }),
            OutputParameters = sqlParams.Where(p => p.Direction == ParameterDirection.Output).ToArray()
        };
    }

    public async Task<MSSQLResponse?> GetListAsync(
        string? category,
        string? searchText,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var sqlParams = new List<SqlParameter>
        {
            new()
            {
                ParameterName = "@category",
                DbType = DbType.String,
                Direction = ParameterDirection.Input,
                Size = 100,
                Value = Utils.IIFStringOrDBNull(category)
            },
            new()
            {
                ParameterName = "@search_text",
                DbType = DbType.String,
                Direction = ParameterDirection.Input,
                Size = 500,
                Value = Utils.IIFStringOrDBNull(searchText)
            },
            new()
            {
                ParameterName = "@page_number",
                DbType = DbType.Int32,
                Direction = ParameterDirection.Input,
                Value = pageNumber < 1 ? 1 : pageNumber
            },
            new()
            {
                ParameterName = "@page_size",
                DbType = DbType.Int32,
                Direction = ParameterDirection.Input,
                Value = pageSize < 1 ? 10 : pageSize
            }
        };

        sqlParams.AddRange(CreateOutputParams());

        return new MSSQLResponse
        {
            Data = await _sqlHelper.FetchData(new ExecuteDataSetRequest
            {
                CommandText = "[dbo].[Get_Document_Mstr_List]",
                CommandTimeout = SqlCommon.SQLCommandTimeOut,
                CommandType = CommandType.StoredProcedure,
                ConnectionProperties = _serviceContext.SQLConnectionModel,
                IsMultipleTables = true,
                Parameters = sqlParams.ToArray()
            }),
            OutputParameters = sqlParams.Where(p => p.Direction == ParameterDirection.Output).ToArray()
        };
    }

    public async Task<MSSQLResponse?> InsertAsync(
        string? category,
        string? name,
        string? path,
        string? createdBy,
        CancellationToken cancellationToken = default)
    {
        var sqlParams = new List<SqlParameter>
        {
            new()
            {
                ParameterName = "@dm_category",
                DbType = DbType.String,
                Direction = ParameterDirection.Input,
                Size = 100,
                Value = Utils.IIFStringOrDBNull(category)
            },
            new()
            {
                ParameterName = "@dm_name",
                DbType = DbType.String,
                Direction = ParameterDirection.Input,
                Size = 500,
                Value = Utils.IIFStringOrDBNull(name)
            },
            new()
            {
                ParameterName = "@dm_path",
                DbType = DbType.String,
                Direction = ParameterDirection.Input,
                Size = -1,
                Value = Utils.IIFStringOrDBNull(path)
            },
            new()
            {
                ParameterName = "@dm_created_by",
                DbType = DbType.String,
                Direction = ParameterDirection.Input,
                Size = 20,
                Value = Utils.IIFStringOrDBNull(createdBy)
            },
            new()
            {
                ParameterName = "@dm_id",
                DbType = DbType.Int64,
                Direction = ParameterDirection.Output
            }
        };

        sqlParams.AddRange(CreateOutputParams());

        var rowsAffected = await _sqlHelper.ExecuteNonQuery(new ExecuteNonQueryRequest
        {
            CommandText = "[dbo].[Insert_Document_Mstr]",
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

    public async Task<MSSQLResponse?> UpdateActiveAsync(long documentId, string active, CancellationToken cancellationToken = default)
    {
        var sqlParams = new List<SqlParameter>
        {
            new()
            {
                ParameterName = "@dm_id",
                DbType = DbType.Int64,
                Direction = ParameterDirection.Input,
                Value = documentId
            },
            new()
            {
                ParameterName = "@dm_active",
                DbType = DbType.StringFixedLength,
                Direction = ParameterDirection.Input,
                Size = 1,
                Value = active
            }
        };

        sqlParams.AddRange(CreateOutputParams());

        var rowsAffected = await _sqlHelper.ExecuteNonQuery(new ExecuteNonQueryRequest
        {
            CommandText = "[dbo].[Update_Document_Mstr_Active]",
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

    public async Task<MSSQLResponse?> GetByIdAsync(long documentId, CancellationToken cancellationToken = default)
    {
        var sqlParams = new List<SqlParameter>
        {
            new()
            {
                ParameterName = "@dm_id",
                DbType = DbType.Int64,
                Direction = ParameterDirection.Input,
                Value = documentId
            }
        };

        sqlParams.AddRange(CreateOutputParams());

        return new MSSQLResponse
        {
            Data = await _sqlHelper.FetchData(new ExecuteDataSetRequest
            {
                CommandText = "[dbo].[Get_Document_Mstr_By_Id]",
                CommandTimeout = SqlCommon.SQLCommandTimeOut,
                CommandType = CommandType.StoredProcedure,
                ConnectionProperties = _serviceContext.SQLConnectionModel,
                IsMultipleTables = true,
                Parameters = sqlParams.ToArray()
            }),
            OutputParameters = sqlParams.Where(p => p.Direction == ParameterDirection.Output).ToArray()
        };
    }

    public async Task<MSSQLResponse?> UpdateIngestionAsync(
        long documentId,
        string ingestionStatus,
        DateTime? ingestedAt,
        string? ingestionError,
        int? chunkCount,
        CancellationToken cancellationToken = default)
    {
        var sqlParams = new List<SqlParameter>
        {
            new()
            {
                ParameterName = "@dm_id",
                DbType = DbType.Int64,
                Direction = ParameterDirection.Input,
                Value = documentId
            },
            new()
            {
                ParameterName = "@dm_ingestion_status",
                DbType = DbType.String,
                Direction = ParameterDirection.Input,
                Size = 20,
                Value = Utils.IIFStringOrDBNull(ingestionStatus)
            },
            new()
            {
                ParameterName = "@dm_ingested_at",
                DbType = DbType.DateTime,
                Direction = ParameterDirection.Input,
                Value = ingestedAt.HasValue ? ingestedAt.Value : DBNull.Value
            },
            new()
            {
                ParameterName = "@dm_ingestion_error",
                DbType = DbType.String,
                Direction = ParameterDirection.Input,
                Size = -1,
                Value = Utils.IIFStringOrDBNull(ingestionError)
            },
            new()
            {
                ParameterName = "@dm_chunk_count",
                DbType = DbType.Int32,
                Direction = ParameterDirection.Input,
                Value = chunkCount.HasValue ? chunkCount.Value : DBNull.Value
            }
        };

        sqlParams.AddRange(CreateOutputParams());

        var rowsAffected = await _sqlHelper.ExecuteNonQuery(new ExecuteNonQueryRequest
        {
            CommandText = "[dbo].[Update_Document_Mstr_Ingestion]",
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
