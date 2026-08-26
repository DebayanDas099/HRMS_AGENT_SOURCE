using Microsoft.Data.SqlClient;
using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Models;

public class MSSQLResponse
{
    [JsonProperty("data")]
    public dynamic? Data { get; set; }

    [JsonProperty("rows_affected")]
    public int? RowsAffected { get; set; }

    [JsonProperty("output_parameters")]
    public SqlParameter[]? OutputParameters { get; set; }
}
