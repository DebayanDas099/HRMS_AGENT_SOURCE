using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

namespace HRMS_CHATBOT_SOURCE.Logic.Common;

public interface ICommonLogic
{
    DateTime GetReferenceDateTime(DateTime? utcNow = null);

    RelativeDateParseResult ParseRelativeDate(string? phrase, DateTime? referenceDate = null);

    RelativeDateParseResult ParseRelativeDateFromUserMessage(string? message, DateTime? referenceDate = null);

    Task<string?> GetUserEmailByMobileAsync(string? mobile, CancellationToken cancellationToken = default);

    Task<int> SendMailNewAsync(
        string toAddress,
        string mailSubject,
        string mailBody,
        string? attachmentPath = null,
        string? ccAddress = null,
        string? bccAddress = null,
        string? fromAddress = null,
        string? senderApp = null,
        string? senderTask = null,
        CancellationToken cancellationToken = default);
}
