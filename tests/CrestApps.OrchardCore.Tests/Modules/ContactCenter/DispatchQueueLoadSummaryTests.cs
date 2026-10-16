using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// What a supervisor reads about what is loaded in a campaign's queue: the profiles its items are stamped with, the kind
/// of work pending, and where the two disagree.
/// </summary>
public sealed class DispatchQueueLoadSummaryTests
{
    // Live: hundreds of items sat in one queue stamped with a turned-off Preview profile while their activities had none.
    // The stamp is what the gate trusts, so it is what has to be shown, with the profile's name, mode and state.
    [Fact]
    public void Stamps_NameEachProfileItsModeAndWhetherItIsOn_AndCountTheItemsStampedWithIt()
    {
        // Arrange
        var preview = new DialerProfile { ItemId = "preview-1", Name = "Leads preview", Mode = DialerMode.Preview, Enabled = false };
        var power = new DialerProfile { ItemId = "power-1", Name = "Leads power", Mode = DialerMode.Power, Enabled = true };
        var items = new[]
        {
            Item("a", "preview-1"), Item("b", "preview-1"), Item("c", "preview-1"),
            Item("d", "power-1"),
        };

        // Act
        var stamps = DispatchQueueLoadSummary.Stamps(items, id => id == "preview-1" ? preview : id == "power-1" ? power : null);

        // Assert
        Assert.Collection(
            stamps,
            first =>
            {
                Assert.Equal("preview-1", first.ProfileId);
                Assert.Equal("Leads preview", first.ProfileName);
                Assert.Equal("Preview", first.Mode);
                Assert.False(first.Enabled);
                Assert.Equal(3, first.ItemCount);
            },
            second =>
            {
                Assert.Equal("Leads power", second.ProfileName);
                Assert.True(second.Enabled);
                Assert.Equal(1, second.ItemCount);
            });
    }

    [Fact]
    public void Stamps_AnItemWithNoProfile_IsCountedApart_AndAProfileThatNoLongerExistsIsLeftUnnamed()
    {
        // Act
        var stamps = DispatchQueueLoadSummary.Stamps([Item("a", null), Item("b", ""), Item("c", "gone-1")], _ => null);

        // Assert
        var none = Assert.Single(stamps, stamp => stamp.ProfileId is null);
        Assert.Equal(2, none.ItemCount);
        var gone = Assert.Single(stamps, stamp => stamp.ProfileId == "gone-1");
        Assert.Null(gone.ProfileName);
        Assert.Null(gone.Enabled);
    }

    [Fact]
    public void Mix_GroupsPendingActivitiesBySourceInteractionTypeAndChannel_LargestFirst()
    {
        // Arrange
        PendingRecord[] records =
        [
            new("1", "Dialer", "Automated", "Phone", null),
            new("2", "Dialer", "Automated", "Phone", null),
            new("3", "Manual", "Manual", "Phone", null),
        ];

        // Act
        var mix = DispatchQueueLoadSummary.Mix(records);

        // Assert
        Assert.Equal(
            [("Dialer", "Automated", "Phone", 2), ("Manual", "Manual", "Phone", 1)],
            mix.Select(group => (group.Source, group.InteractionType, group.Channel, group.Count)));
    }

    // Live: the activities carried no profile, and the stamp on their queue items was a turned-off one.
    [Fact]
    public void Compare_FlagsAnItemStampedDifferentlyFromItsActivity_AndAnActivityWithNoProfile()
    {
        // Arrange
        var waiting = new Dictionary<string, QueueItem>(StringComparer.Ordinal)
        {
            ["a1"] = Item("a1", "preview-1"),
            ["a2"] = Item("a2", "power-1"),
            ["a3"] = Item("a3", "power-1"),
        };
        PendingRecord[] records =
        [
            new("a1", "Dialer", "Automated", "Phone", null),
            new("a2", "Dialer", "Automated", "Phone", "power-1"),
            new("a3", "Dialer", "Automated", "Phone", "preview-1"),
            new("a4", "Dialer", "Automated", "Phone", null),
        ];

        // Act
        var (mismatched, withoutProfile) = DispatchQueueLoadSummary.Compare(waiting, records);

        // Assert
        Assert.Equal(2, mismatched);
        Assert.Equal(2, withoutProfile);
    }

    [Fact]
    public void Compare_AnItemAndItsActivityThatAgree_RaiseNothing()
    {
        // Act
        var (mismatched, withoutProfile) = DispatchQueueLoadSummary.Compare(
            new Dictionary<string, QueueItem> { ["a1"] = Item("a1", "power-1") },
            [new PendingRecord("a1", "Dialer", "Automated", "Phone", "power-1")]);

        // Assert
        Assert.Equal(0, mismatched);
        Assert.Equal(0, withoutProfile);
    }

    [Fact]
    public void Pacing_WithNothingWaiting_HasNothingToPace()
        => Assert.Equal(DispatchPacing.NothingWaiting, DispatchQueueLoadSummary.Pacing(null, null));

    // The pacer only runs for a queue whose head item carries an enabled Power, Progressive or paced Predictive profile.
    [Theory]
    [InlineData(DialerMode.Power, true, DispatchPacing.Paced)]
    [InlineData(DialerMode.Progressive, true, DispatchPacing.Paced)]
    [InlineData(DialerMode.Predictive, true, DispatchPacing.Paced)]
    [InlineData(DialerMode.Preview, true, DispatchPacing.AgentDriven)]
    [InlineData(DialerMode.Power, false, DispatchPacing.ProfileDisabled)]
    [InlineData(DialerMode.Preview, false, DispatchPacing.ProfileDisabled)]
    public void Pacing_FollowsTheProfileStampedOnTheHeadItem(DialerMode mode, bool enabled, DispatchPacing expected)
    {
        // Arrange
        var profile = new DialerProfile { ItemId = "p-1", Name = "Profile", Mode = mode, Enabled = enabled };

        // Act
        var pacing = DispatchQueueLoadSummary.Pacing(Item("a", "p-1"), profile);

        // Assert
        Assert.Equal(expected, pacing);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("gone-1")]
    public void Pacing_WhenTheHeadItemsProfileCannotBeRead_IsMissing(string stamp)
        => Assert.Equal(DispatchPacing.ProfileMissing, DispatchQueueLoadSummary.Pacing(Item("a", stamp), headProfile: null));

    [Fact]
    public void Pacing_ForAQueuedCallback_IsAgentDriven()
        => Assert.Equal(DispatchPacing.AgentDriven, DispatchQueueLoadSummary.Pacing(Item("a", QueueCallbackDialerProfile.Id), headProfile: null));

    private static QueueItem Item(string activityId, string profileId)
        => new()
        {
            ItemId = $"item-{activityId}",
            ActivityItemId = activityId,
            DialerProfileId = profileId,
            QueueId = "__campaign-queue__c1",
        };
}
