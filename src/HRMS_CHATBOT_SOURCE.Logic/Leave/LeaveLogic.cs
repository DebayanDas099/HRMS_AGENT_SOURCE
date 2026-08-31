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

    public Task<string> ValidateAndApplyLeaveAsync(
        string? mobile,
        DateTime? fromDate,
        DateTime? toDate,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mobile))
        {
            throw new ValidationException("Mobile number is required.");
        }

        // Real validation and submission against the leave backend is not wired up yet.
        return Task.FromResult(
            "Leave validation and application is not available yet. "
            + "This request has been noted, but no leave has been submitted or validated.");
    }
}
