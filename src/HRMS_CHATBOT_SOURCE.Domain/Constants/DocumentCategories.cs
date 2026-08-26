namespace HRMS_CHATBOT_SOURCE.Domain.Constants;

public static class DocumentCategories
{
    public const string Policy = "Policy";
    public const string Training = "Training";

    public static readonly string[] Allowed =
    [
        Policy,
        Training
    ];
}
