using HRMS_CHATBOT_SOURCE.Agent.Notifications;

namespace HRMS_CHATBOT_SOURCE.Tests.Agent;

public class InMemoryAdminNotificationStoreTests
{
    [Fact]
    public async Task GetUnreadCountAsync_NoNotifications_ReturnsZero()
    {
        var store = new InMemoryAdminNotificationStore();

        Assert.Equal(0, await store.GetUnreadCountAsync());
    }

    [Fact]
    public async Task AddAsync_IncrementsTheUnreadCount()
    {
        var store = new InMemoryAdminNotificationStore();

        await store.AddAsync(applicationReference: null, "9999999999", "Alice");
        await store.AddAsync(applicationReference: null, "8888888888", "Bob");

        Assert.Equal(2, await store.GetUnreadCountAsync());
    }

    [Fact]
    public async Task MarkAllReadAsync_ClearsTheUnreadCount()
    {
        var store = new InMemoryAdminNotificationStore();
        await store.AddAsync(applicationReference: null, "9999999999", "Alice");

        await store.MarkAllReadAsync();

        Assert.Equal(0, await store.GetUnreadCountAsync());
    }
}
