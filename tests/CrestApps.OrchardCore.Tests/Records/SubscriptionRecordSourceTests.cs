using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Subscriptions.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Moq;

namespace CrestApps.OrchardCore.Tests.Records;

/// <summary>
/// Subscription agreements are a record kind: a user who manages subscriptions may search them, and any record that
/// links to one shows its title and the status the subscription screens show.
/// </summary>
public sealed class SubscriptionRecordSourceTests
{
    [Fact]
    public async Task ASearch_ReturnsSubscriptions_OnlyForAUserWhoManagesSubscriptions()
    {
        // Arrange
        var source = CreateSource();
        var manager = PermissionClaimAuthorizationService.User(SubscriptionPermissions.ManageSubscriptions);
        var customer = PermissionClaimAuthorizationService.User(SubscriptionPermissions.ManageOwnSubscriptions);

        // Act
        var found = await source.SearchAsync(new RecordSearch { User = manager, Count = 1 }, TestContext.Current.CancellationToken);
        var forbidden = await source.SearchAsync(new RecordSearch { User = customer }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(found);
        Assert.Empty(forbidden);
        Assert.True(await source.CanSearchAsync(manager, TestContext.Current.CancellationToken));
        Assert.False(await source.CanSearchAsync(customer, TestContext.Current.CancellationToken));
        Assert.False(await source.CanSearchAsync(null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheSummaries_DescribeTheSubscriptionsFound_ByTheirIds()
    {
        // Arrange
        var source = CreateSource();

        // Act
        var summaries = await source.GetSummariesAsync(["s2", "missing"], TestContext.Current.CancellationToken);
        var nothing = await source.GetSummariesAsync(null, TestContext.Current.CancellationToken);

        // Assert
        var summary = Assert.Single(summaries);

        Assert.Equal("s2", summary.Key);
        Assert.Equal("Gold plan", summary.Value.Title);
        Assert.Equal("Past due", summary.Value.Description);
        Assert.Equal("/Admin/subscription-agreements/Detail/s2", summary.Value.AdminUrl);
        Assert.Empty(nothing);
        Assert.Equal(SubscriptionConstants.RecordKind, source.Kind);
    }

    private static SubscriptionRecordSource CreateSource()
    {
        var store = new InMemorySubscriptionStore(
            new Subscription
            {
                ItemId = "s1",
                Title = "Silver plan",
                Status = SubscriptionStatus.Active,
            },
            new Subscription
            {
                ItemId = "s2",
                Title = "Gold plan",
                Status = SubscriptionStatus.PastDue,
            });

        var httpContextAccessor = new Mock<IHttpContextAccessor>();

        httpContextAccessor.SetupGet(accessor => accessor.HttpContext).Returns(new DefaultHttpContext());

        return new SubscriptionRecordSource(
            store,
            new SubscriptionDisplayText([], new StringLocalizer<SubscriptionDisplayText>(new SourceTextLocalizerFactory())),
            new PermissionClaimAuthorizationService(),
            new TemplateLinkGenerator((_, values) => $"/Admin/subscription-agreements/{values["action"]}/{values["itemId"]}"),
            httpContextAccessor.Object);
    }
}
