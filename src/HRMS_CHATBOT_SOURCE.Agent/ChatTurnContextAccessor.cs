namespace HRMS_CHATBOT_SOURCE.Agent;

public interface IChatTurnContextAccessor
{
    string? Mobile { get; }
    string? BaseUrl { get; }
    void Set(string? mobile, string? baseUrl);
    void Clear();
}

public sealed class ChatTurnContextAccessor : IChatTurnContextAccessor
{
    private static readonly AsyncLocal<ChatTurnContext?> Context = new();

    public string? Mobile => Context.Value?.Mobile;
    public string? BaseUrl => Context.Value?.BaseUrl;

    public void Set(string? mobile, string? baseUrl)
    {
        Context.Value = new ChatTurnContext(mobile?.Trim(), baseUrl?.Trim());
    }

    public void Clear()
    {
        Context.Value = null;
    }

    private sealed record ChatTurnContext(string? Mobile, string? BaseUrl);
}
