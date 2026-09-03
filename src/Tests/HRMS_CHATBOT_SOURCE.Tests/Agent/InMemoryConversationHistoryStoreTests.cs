using HRMS_CHATBOT_SOURCE.Agent.History;
using Microsoft.Extensions.AI;

namespace HRMS_CHATBOT_SOURCE.Tests.Agent;

public class InMemoryConversationHistoryStoreTests
{
    [Fact]
    public async Task GetHistoryAsync_UnknownConversation_ReturnsEmpty()
    {
        var store = new InMemoryConversationHistoryStore();

        var history = await store.GetHistoryAsync("conversation-1");

        Assert.Empty(history);
    }

    [Fact]
    public async Task SaveThenGet_RoundTripsMessages()
    {
        var store = new InMemoryConversationHistoryStore();
        var messages = new List<ChatMessage> { new(ChatRole.User, "hello") };

        await store.SaveHistoryAsync("conversation-1", messages);
        var retrieved = await store.GetHistoryAsync("conversation-1");

        Assert.Equal("hello", Assert.Single(retrieved).Text);
    }

    [Fact]
    public async Task SaveHistoryAsync_OverwritesThePreviousSnapshot()
    {
        var store = new InMemoryConversationHistoryStore();
        await store.SaveHistoryAsync("conversation-1", [new ChatMessage(ChatRole.User, "first")]);

        await store.SaveHistoryAsync(
            "conversation-1",
            [new ChatMessage(ChatRole.User, "first"), new ChatMessage(ChatRole.Assistant, "second")]);

        var history = await store.GetHistoryAsync("conversation-1");
        Assert.Equal(2, history.Count);
    }

    [Fact]
    public async Task GetHistoryAsync_IsScopedToItsConversation()
    {
        var store = new InMemoryConversationHistoryStore();
        await store.SaveHistoryAsync("conversation-1", [new ChatMessage(ChatRole.User, "a")]);
        await store.SaveHistoryAsync("conversation-2", [new ChatMessage(ChatRole.User, "b")]);

        var history = await store.GetHistoryAsync("conversation-1");

        Assert.Equal("a", Assert.Single(history).Text);
    }
}
