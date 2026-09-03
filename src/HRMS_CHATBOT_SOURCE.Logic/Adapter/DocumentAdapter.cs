using System.ComponentModel.DataAnnotations;
using System.Data;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Domain.Models;

namespace HRMS_CHATBOT_SOURCE.Logic.Adapter;

internal static class DocumentAdapter
{
    internal static DocumentStatisticsDto MapStatistics(MSSQLResponse? response)
    {
        EnsureSuccess(response);

        if (response?.Data is not DataSet { Tables.Count: > 0 } dataSet
            || dataSet.Tables[0].Rows.Count == 0)
        {
            return new DocumentStatisticsDto();
        }

        var row = dataSet.Tables[0].Rows[0];
        return new DocumentStatisticsDto
        {
            TotalDocuments = ToInt(row["total_documents"]),
            PolicyDocuments = ToInt(row["policy_documents"]),
            TrainingDocuments = ToInt(row["training_documents"]),
            ActiveDocuments = ToInt(row["active_documents"]),
            InactiveDocuments = ToInt(row["inactive_documents"])
        };
    }

    internal static List<DocumentMstrDto> MapList(MSSQLResponse? response)
    {
        return MapPagedList(response, 1, 10).Items;
    }

    internal static DocumentListResponseDto MapPagedList(MSSQLResponse? response, int pageNumber, int pageSize)
    {
        EnsureSuccess(response);

        var result = new DocumentListResponseDto
        {
            PageNumber = pageNumber < 1 ? 1 : pageNumber,
            PageSize = pageSize < 1 ? 10 : pageSize
        };

        if (response?.Data is not DataSet { Tables.Count: > 0 } dataSet)
        {
            return result;
        }

        if (dataSet.Tables.Count > 0 && dataSet.Tables[0].Rows.Count > 0
            && dataSet.Tables[0].Columns.Contains("total_count"))
        {
            result.TotalCount = ToInt(dataSet.Tables[0].Rows[0]["total_count"]);
        }

        var itemsTableIndex = dataSet.Tables.Count > 1 ? 1 : 0;
        if (dataSet.Tables.Count > itemsTableIndex
            && dataSet.Tables[itemsTableIndex].Rows.Count > 0
            && dataSet.Tables[itemsTableIndex].Columns.Contains("dm_id"))
        {
            result.Items = dataSet.Tables[itemsTableIndex].Rows.Cast<DataRow>().Select(MapDocumentRow).ToList();
        }

        result.TotalPages = result.PageSize <= 0
            ? 0
            : (int)Math.Ceiling(result.TotalCount / (double)result.PageSize);

        return result;
    }

    internal static DocumentMstrDto MapDocumentRow(DataRow row)
    {
        return new DocumentMstrDto
        {
            DocumentId = Convert.ToInt64(row["dm_id"]),
            Category = Convert.ToString(row["dm_category"]) ?? string.Empty,
            Name = Convert.ToString(row["dm_name"]) ?? string.Empty,
            Path = Convert.ToString(row["dm_path"]) ?? string.Empty,
            CreatedBy = Convert.ToString(row["dm_created_by"]) ?? string.Empty,
            CreatedDate = row["dm_created_date"] is DateTime createdDate ? createdDate : Convert.ToDateTime(row["dm_created_date"]),
            Active = Convert.ToString(row["dm_active"]) ?? "N",
            IngestionStatus = ReadOptionalString(row, "dm_ingestion_status") ?? "Pending",
            IngestedAt = ReadOptionalDateTime(row, "dm_ingested_at"),
            IngestionError = ReadOptionalString(row, "dm_ingestion_error"),
            ChunkCount = ReadOptionalInt(row, "dm_chunk_count")
        };
    }

    internal static DocumentMstrDto? MapById(MSSQLResponse? response)
    {
        EnsureSuccess(response);

        if (response?.Data is not DataSet { Tables.Count: > 0 } dataSet
            || dataSet.Tables[0].Rows.Count == 0
            || !dataSet.Tables[0].Columns.Contains("dm_id"))
        {
            return null;
        }

        return MapDocumentRow(dataSet.Tables[0].Rows[0]);
    }

    internal static IReadOnlyList<DocumentSimilarityMatchDto> MapSimilarityMatches(MSSQLResponse? response)
    {
        EnsureSuccess(response);

        if (response?.Data is not DataSet { Tables.Count: > 0 } dataSet
            || dataSet.Tables[0].Rows.Count == 0
            || !dataSet.Tables[0].Columns.Contains("dm_id"))
        {
            return [];
        }

        return dataSet.Tables[0].Rows.Cast<DataRow>().Select(row => new DocumentSimilarityMatchDto
        {
            DocumentId = Convert.ToInt64(row["dm_id"]),
            Category = Convert.ToString(row["dm_category"]) ?? string.Empty,
            Name = Convert.ToString(row["dm_name"]) ?? string.Empty,
            Path = Convert.ToString(row["dm_path"]) ?? string.Empty,
            Active = Convert.ToString(row["dm_active"]) ?? "N",
            IngestionStatus = ReadOptionalString(row, "dm_ingestion_status") ?? "Pending",
            CreatedDate = ReadOptionalDateTime(row, "dm_created_date"),
            SimilarityScore = row.Table.Columns.Contains("similarity_score")
                ? ToInt(row["similarity_score"])
                : 0
        }).ToList();
    }

    internal static long MapInsertedDocumentId(MSSQLResponse? response)
    {
        EnsureSuccess(response);

        var documentIdValue = response?.OutputParameters?.FirstOrDefault(p =>
            string.Equals(p.ParameterName, "@dm_id", StringComparison.OrdinalIgnoreCase))?.Value;

        return documentIdValue == null || documentIdValue == DBNull.Value
            ? 0
            : Convert.ToInt64(documentIdValue);
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
            throw new ValidationException(string.IsNullOrWhiteSpace(outputMsg) ? "Document operation failed." : outputMsg);
        }
    }

    private static int ToInt(object? value)
    {
        return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
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
}
