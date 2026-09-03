using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

namespace HRMS_CHATBOT_SOURCE.Logic;

public interface ILeaveLogic
{
    Task<IReadOnlyList<LeaveBalanceCategoryDto>> GetLeaveBalanceSummaryAsync(
        string? mobile,
        DateTime? startDate,
        DateTime? endDate,
        string? leaveCategory,
        CancellationToken cancellationToken = default);

    Task<string> ValidateAndApplyLeaveAsync(
        string? mobile,
        DateTime fromDate,
        DateTime toDate,
        string? leaveType,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HolidayListItemDto>> GetHolidayListAsync(
        DateTime? startDate,
        DateTime? endDate,
        int? maxResults,
        CancellationToken cancellationToken = default);
}
