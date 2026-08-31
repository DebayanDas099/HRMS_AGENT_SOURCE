using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

namespace HRMS_CHATBOT_SOURCE.Logic;

public interface ILeaveLogic
{
    Task<LeaveBalanceSummaryDto> GetLeaveBalanceSummaryAsync(
        string? mobile,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default);

    Task<string> ValidateAndApplyLeaveAsync(
        string? mobile,
        DateTime? fromDate,
        DateTime? toDate,
        string? reason,
        CancellationToken cancellationToken = default);
}
