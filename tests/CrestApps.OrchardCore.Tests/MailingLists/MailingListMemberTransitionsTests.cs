using CrestApps.OrchardCore.MailingLists.Core.Services;
using CrestApps.OrchardCore.MailingLists.Models;

namespace CrestApps.OrchardCore.Tests.MailingLists;

/// <summary>
/// The moves a mailing list member may make: a bounced address must confirm again, and nothing leaves Pending except a
/// confirmation or an unsubscribe.
/// </summary>
public sealed class MailingListMemberTransitionsTests
{
    [Theory]
    [InlineData(MailingListMemberStatus.Pending, MailingListMemberStatus.Subscribed)]
    [InlineData(MailingListMemberStatus.Pending, MailingListMemberStatus.Unsubscribed)]
    [InlineData(MailingListMemberStatus.Subscribed, MailingListMemberStatus.Unsubscribed)]
    [InlineData(MailingListMemberStatus.Subscribed, MailingListMemberStatus.Cleaned)]
    [InlineData(MailingListMemberStatus.Unsubscribed, MailingListMemberStatus.Pending)]
    [InlineData(MailingListMemberStatus.Unsubscribed, MailingListMemberStatus.Subscribed)]
    [InlineData(MailingListMemberStatus.Cleaned, MailingListMemberStatus.Pending)]
    public void CanMove_AnAllowedMove_IsAllowed(MailingListMemberStatus from, MailingListMemberStatus to)
    {
        // Act
        var allowed = MailingListMemberTransitions.CanMove(from, to);

        // Assert
        Assert.True(allowed);
    }

    [Theory]
    [InlineData(MailingListMemberStatus.Pending, MailingListMemberStatus.Cleaned)]
    [InlineData(MailingListMemberStatus.Subscribed, MailingListMemberStatus.Pending)]
    [InlineData(MailingListMemberStatus.Unsubscribed, MailingListMemberStatus.Cleaned)]
    [InlineData(MailingListMemberStatus.Cleaned, MailingListMemberStatus.Subscribed)]
    [InlineData(MailingListMemberStatus.Cleaned, MailingListMemberStatus.Unsubscribed)]
    [InlineData(MailingListMemberStatus.Subscribed, MailingListMemberStatus.Subscribed)]
    public void CanMove_AMoveOutsideTheTable_IsRefused(MailingListMemberStatus from, MailingListMemberStatus to)
    {
        // Act
        var allowed = MailingListMemberTransitions.CanMove(from, to);

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public void Table_HasNoTerminalState_SoEveryAddressCanComeBack()
    {
        // Act
        var terminal = Enum.GetValues<MailingListMemberStatus>().Where(MailingListMemberTransitions.Table.IsTerminal);

        // Assert
        Assert.Empty(terminal);
    }
}
