using HRMS_CHATBOT_SOURCE.Domain.Models;

namespace HRMS_CHATBOT_SOURCE.Repo.Leave;

public interface ILeaveRepo
{
    Task<MSSQLResponse?> GetLeaveDetailsByUserAsync(
        string? mobile,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    Task<MSSQLResponse?> ValidateAndApplyLeaveByUserAsync(
        string? mobile,
        DateTime startDate,
        DateTime endDate,
        string? leaveType,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<MSSQLResponse?> GetHolidayListAsync(
        DateTime startDate,
        DateTime endDate,
        int? maxResults,
        CancellationToken cancellationToken = default);
}
