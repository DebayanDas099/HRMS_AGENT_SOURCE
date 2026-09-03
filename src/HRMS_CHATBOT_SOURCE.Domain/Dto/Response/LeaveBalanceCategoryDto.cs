using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class LeaveBalanceCategoryDto
{
    [JsonProperty("emp_id")]
    public string EmpId { get; set; } = string.Empty;

    [JsonProperty("leave_category")]
    public string LeaveCategory { get; set; } = string.Empty;

    [JsonProperty("category_value")]
    public decimal CategoryValue { get; set; }

    [JsonProperty("loss_of_pay")]
    public decimal LossOfPay { get; set; }

    [JsonProperty("contract_end_date")]
    public DateTime? ContractEndDate { get; set; }

    [JsonProperty("days_until_contract_end")]
    public int? DaysUntilContractEnd { get; set; }

    [JsonProperty("contract_status")]
    public string? ContractStatus { get; set; }
}
