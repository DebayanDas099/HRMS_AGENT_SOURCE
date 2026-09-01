using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

namespace HRMS_CHATBOT_SOURCE.Logic.Common;

public interface ICommonLogic
{
    DateTime GetReferenceDateTime(DateTime? utcNow = null);

    RelativeDateParseResult ParseRelativeDate(string? phrase, DateTime? referenceDate = null);

    RelativeDateParseResult ParseRelativeDateFromUserMessage(string? message, DateTime? referenceDate = null);
}
