using CrestApps.OrchardCore.MailingLists.Core.Services;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;

namespace CrestApps.OrchardCore.Tests.MailingLists;

/// <summary>
/// A change a provider reports is ignored when it is older than the member's last status change, so a late webhook
/// never subscribes someone again; a change at the same moment or later is applied, within the transition table. A
/// change of a member who never signed up on the site runs silently.
/// </summary>
public sealed class MailingListProviderChangesTests
{
    private static readonly DateTime _changed = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsStale_AChangeOlderThanTheLastStatusChange_IsStale()
    {
        // Arrange
        var member = new MailingListMember { Status = MailingListMemberStatus.Unsubscribed, StatusChangedUtc = _changed };

        // Act
        var stale = MailingListProviderChanges.IsStale(member, Change(MailingListMemberStatus.Subscribed, _changed.AddSeconds(-1)));

        // Assert
        Assert.True(stale);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void IsStale_AChangeAtTheSameMomentOrLater_IsApplied(int seconds)
    {
        // Arrange
        var member = new MailingListMember { Status = MailingListMemberStatus.Subscribed, StatusChangedUtc = _changed };

        // Act
        var stale = MailingListProviderChanges.IsStale(member, Change(MailingListMemberStatus.Unsubscribed, _changed.AddSeconds(seconds)));

        // Assert
        Assert.False(stale);
    }

    [Fact]
    public void IsStale_AStoredTimeWithoutKind_IsReadAsUtc()
    {
        // Arrange
        var member = new MailingListMember { StatusChangedUtc = DateTime.SpecifyKind(_changed, DateTimeKind.Unspecified) };

        // Act
        var stale = MailingListProviderChanges.IsStale(member, Change(MailingListMemberStatus.Subscribed, _changed.AddMinutes(-5)));

        // Assert
        Assert.True(stale);
    }

    [Fact]
    public void IsStale_AnAddressNotOnTheList_IsNeverStale()
    {
        // Act
        var stale = MailingListProviderChanges.IsStale(null, Change(MailingListMemberStatus.Subscribed, _changed));

        // Assert
        Assert.False(stale);
    }

    [Fact]
    public void NextStatus_ACleanedAddressReportedSubscribed_StaysCleaned()
    {
        // Arrange
        var member = new MailingListMember { Status = MailingListMemberStatus.Cleaned };

        // Act
        var next = MailingListProviderChanges.NextStatus(member, Change(MailingListMemberStatus.Subscribed, _changed));

        // Assert
        Assert.Null(next);
    }

    [Fact]
    public void NextStatus_AnUnsubscribe_MovesASubscribedMember()
    {
        // Arrange
        var member = new MailingListMember { Status = MailingListMemberStatus.Subscribed };

        // Act
        var next = MailingListProviderChanges.NextStatus(member, Change(MailingListMemberStatus.Unsubscribed, _changed));

        // Assert
        Assert.Equal(MailingListMemberStatus.Unsubscribed, next);
    }

    [Fact]
    public void NextStatus_AProfileOnlyChange_MovesNothing()
    {
        // Arrange
        var member = new MailingListMember { Status = MailingListMemberStatus.Subscribed };

        // Act
        var next = MailingListProviderChanges.NextStatus(member, new MailingListProviderChange { ListId = "list1", Email = "ada@example.com", Name = "Ada", OccurredUtc = _changed });

        // Assert
        Assert.Null(next);
    }

    [Theory]
    [InlineData(MailingListMemberSources.Provider)]
    [InlineData(MailingListMemberSources.Admin)]
    [InlineData(MailingListMemberSources.Import)]
    public void IsSilent_AMemberWhoNeverSignedUpOnTheSite_IsSilent(string source)
    {
        // Arrange
        var member = new MailingListMember { Source = source };

        // Act
        var silent = MailingListProviderChanges.IsSilent(member);

        // Assert
        Assert.True(silent);
    }

    [Theory]
    [InlineData(MailingListMemberSources.Widget)]
    [InlineData(MailingListMemberSources.Workflow)]
    [InlineData("Checkout")]
    public void IsSilent_AMemberWhoSignedUpOnTheSite_IsNotSilent(string source)
    {
        // Arrange
        var member = new MailingListMember { Source = source };

        // Act
        var silent = MailingListProviderChanges.IsSilent(member);

        // Assert
        Assert.False(silent);
    }

    private static MailingListProviderChange Change(MailingListMemberStatus status, DateTime occurredUtc)
        => new()
        {
            ListId = "list1",
            Email = "ada@example.com",
            Status = status,
            OccurredUtc = occurredUtc,
        };
}
