using CrestApps.OrchardCore.Customers.BackgroundTasks;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Services;

namespace CrestApps.OrchardCore.Tests.Customers;

/// <summary>
/// The customer retention task reaches the guest records other modules keep, such as guest orders, through
/// <see cref="IGuestRetentionHandler"/>, so Customers applies its retention window without knowing those modules.
/// </summary>
public sealed class GuestRetentionContractTests
{
    private static readonly DateTime _now = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task TheRetentionTask_AsksEveryModuleForRecordsOlderThanTheWindow_AndAddsUpWhatTheyAnonymized()
    {
        // Arrange
        var orders = new RecordingHandler(3);
        var bookings = new RecordingHandler(2);

        // Act
        var anonymized = await CustomerRetentionBackgroundTask.AnonymizeGuestRecordsAsync(new CustomerPrivacySettings { GuestOrderMonths = 12 }, _now, [orders, bookings], TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(5, anonymized);
        Assert.Equal(_now.AddMonths(-12), orders.CreatedBefore);
        Assert.Equal(CustomerRetentionBackgroundTask.BatchSize, orders.Limit);
        Assert.Equal(_now.AddMonths(-12), bookings.CreatedBefore);
    }

    [Fact]
    public async Task WithoutAWindow_NoModuleIsAsked()
    {
        var orders = new RecordingHandler(3);

        var anonymized = await CustomerRetentionBackgroundTask.AnonymizeGuestRecordsAsync(new CustomerPrivacySettings { GuestOrderMonths = 0 }, _now, [orders], TestContext.Current.CancellationToken);

        Assert.Equal(0, anonymized);
        Assert.Null(orders.CreatedBefore);
    }

    private sealed class RecordingHandler : IGuestRetentionHandler
    {
        private readonly int _result;

        public RecordingHandler(int result)
        {
            _result = result;
        }

        public DateTime? CreatedBefore { get; private set; }

        public int Limit { get; private set; }

        public Task<int> AnonymizeGuestRecordsAsync(DateTime createdBeforeUtc, int limit, CancellationToken cancellationToken = default)
        {
            CreatedBefore = createdBeforeUtc;
            Limit = limit;

            return Task.FromResult(_result);
        }
    }
}
