using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class LeaveBalanceSummaryDto
{
    [JsonProperty("emp_id")]
    public string EmpId { get; set; } = string.Empty;

    [JsonProperty("accrued_leave_balance")]
    public decimal AccruedLeaveBalance { get; set; }

    [JsonProperty("applied_leave")]
    public decimal AppliedLeave { get; set; }

    [JsonProperty("remaining_leave_balance")]
    public decimal RemainingLeaveBalance { get; set; }

    [JsonProperty("loss_of_pay")]
    public decimal LossOfPay { get; set; }

    [JsonProperty("contract_end_date")]
    public DateTime? ContractEndDate { get; set; }

    [JsonProperty("days_until_contract_end")]
    public int? DaysUntilContractEnd { get; set; }

    [JsonProperty("contract_status")]
    public string? ContractStatus { get; set; }

    [JsonProperty("leave_type_ld_lov_id_fk")]
    public long LeaveTypeLdLovIdFk { get; set; }
}
