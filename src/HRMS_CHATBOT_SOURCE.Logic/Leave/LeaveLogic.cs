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
        string? reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mobile))
        {
            throw new ValidationException("Mobile number is required.");
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
            reason.Trim(),
            cancellationToken);

        return LeaveAdapter.MapApplyResult(response);
    }
}
