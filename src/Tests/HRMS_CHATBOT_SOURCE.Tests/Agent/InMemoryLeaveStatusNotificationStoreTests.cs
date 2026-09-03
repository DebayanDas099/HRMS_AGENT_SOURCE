using HRMS_CHATBOT_SOURCE.Agent.Notifications;

namespace HRMS_CHATBOT_SOURCE.Tests.Agent;

public class InMemoryLeaveStatusNotificationStoreTests
{
    [Fact]
    public async Task GetPendingAsync_UnknownMobile_ReturnsEmpty()
    {
        var store = new InMemoryLeaveStatusNotificationStore();

        var pending = await store.GetPendingAsync("9999999999");

        Assert.Empty(pending);
    }

    [Fact]
    public async Task AddAsync_ThenGetPendingAsync_RoundTrips()
    {
        var store = new InMemoryLeaveStatusNotificationStore();

        await store.AddAsync("9999999999", "ref-1", "Approved", "Note", default);
        var pending = await store.GetPendingAsync("9999999999");

        var notification = Assert.Single(pending);
        Assert.Equal("ref-1", notification.ApplicationReference);
        Assert.Equal("Approved", notification.NewStatus);
    }

    [Fact]
    public async Task MarkDeliveredAsync_RemovesTheEntryFromPending()
    {
        var store = new InMemoryLeaveStatusNotificationStore();
        await store.AddAsync("9999999999", "ref-1", "Approved", null, default);
        var pending = await store.GetPendingAsync("9999999999");

        await store.MarkDeliveredAsync("9999999999", pending.Select(p => p.Id).ToList());
        var afterDelivery = await store.GetPendingAsync("9999999999");

        Assert.Empty(afterDelivery);
    }

    [Fact]
    public async Task GetPendingAsync_IsScopedToItsMobile()
    {
        var store = new InMemoryLeaveStatusNotificationStore();
        await store.AddAsync("9999999999", "ref-1", "Approved", null, default);
        await store.AddAsync("8888888888", "ref-2", "Rejected", null, default);

        var pending = await store.GetPendingAsync("9999999999");

        Assert.Single(pending);
    }
}
