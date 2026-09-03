using System.ComponentModel.DataAnnotations;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Logic.Adapter;
using HRMS_CHATBOT_SOURCE.Repo.Leave;

namespace HRMS_CHATBOT_SOURCE.Logic;

public class LeaveLogic : ILeaveLogic
{
    private readonly ILeaveRepo _leaveRepo;

    public LeaveLogic(ILeaveRepo leaveRepo)
    {
        _leaveRepo = leaveRepo;
    }

    public async Task<LeaveBalanceSummaryDto> GetLeaveBalanceSummaryAsync(
        string? mobile,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mobile))
        {
            throw new ValidationException("Mobile number is required.");
        }

        var today = DateTime.Today;
        var start = (startDate ?? new DateTime(today.Year, today.Month, 1)).Date;
        var end = (endDate ?? start.AddMonths(1).AddDays(-1)).Date;

        if (start > end)
        {
            throw new ValidationException("Start date cannot be after end date.");
        }

        var response = await _leaveRepo.GetLeaveDetailsByUserAsync(mobile.Trim(), start, end, cancellationToken);
        return LeaveAdapter.MapBalanceSummary(response);
    }

    public async Task<string> ValidateAndApplyLeaveAsync(
        string? mobile,
        DateTime fromDate,
        DateTime toDate,
        string? leaveType,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mobile))
        {
            throw new ValidationException("Mobile number is required.");
        }

        if (string.IsNullOrWhiteSpace(leaveType))
        {
            throw new ValidationException("Leave type is required.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ValidationException("Reason for leave is required.");
        }

        var start = fromDate.Date;
        var end = toDate.Date;

        if (start > end)
        {
            throw new ValidationException("Start date cannot be after end date.");
        }

        var response = await _leaveRepo.ValidateAndApplyLeaveByUserAsync(
            mobile.Trim(),
            start,
            end,
            leaveType.Trim(),
            reason.Trim(),
            cancellationToken);

        return LeaveAdapter.MapApplyResult(response);
    }

    public async Task<IReadOnlyList<HolidayListItemDto>> GetHolidayListAsync(
        DateTime? startDate,
        DateTime? endDate,
        int? maxResults,
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        DateTime start;
        DateTime end;

        if (maxResults.HasValue && !startDate.HasValue && !endDate.HasValue)
        {
            start = today;
            end = new DateTime(today.Year, 12, 31);
        }
        else
        {
            start = (startDate ?? new DateTime(today.Year, 1, 1)).Date;
            end = (endDate ?? new DateTime(start.Year, 12, 31)).Date;
        }

        if (start > end)
        {
            throw new ValidationException("Start date cannot be after end date.");
        }

        var response = await _leaveRepo.GetHolidayListAsync(start, end, maxResults, cancellationToken);
        return LeaveAdapter.MapHolidayList(response);
    }
}
