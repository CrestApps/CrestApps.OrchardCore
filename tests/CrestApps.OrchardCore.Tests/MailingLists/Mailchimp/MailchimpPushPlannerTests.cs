using CrestApps.OrchardCore.MailingLists.Mailchimp.Services;
using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;

namespace CrestApps.OrchardCore.Tests.MailingLists.Mailchimp;

public sealed class MailchimpPushPlannerTests
{
    [Fact]
    public void Plan_PendingMember_OnAListTheProviderConfirms_IsAddedAsPending()
    {
        // Arrange
        var list = MailchimpTestContext.List(MailingListConfirmation.Provider);
        var member = MailchimpTestContext.Member("a@example.com", MailingListMemberStatus.Pending);

        // Act
        var plan = MailchimpPushPlanner.Plan(list, member, MailingListPushKind.Upsert);

        // Assert
        Assert.Equal(MailchimpPushAction.Upsert, plan.Action);
        Assert.Equal("pending", plan.StatusIfNew);
    }

    [Theory]
    [InlineData(MailingListConfirmation.Email)]
    [InlineData(MailingListConfirmation.None)]
    public void Plan_PendingMember_OnAListTheSiteConfirms_SendsNothing(MailingListConfirmation confirmation)
    {
        // Arrange
        var list = MailchimpTestContext.List(confirmation);
        var member = MailchimpTestContext.Member("a@example.com", MailingListMemberStatus.Pending);

        // Act
        var plan = MailchimpPushPlanner.Plan(list, member, MailingListPushKind.Upsert);

        // Assert
        Assert.Equal(MailchimpPushAction.Skip, plan.Action);
    }

    [Fact]
    public void Plan_SubscribedMember_IsAddedAsSubscribed()
    {
        // Arrange
        var list = MailchimpTestContext.List();
        var member = MailchimpTestContext.Member("a@example.com", MailingListMemberStatus.Subscribed);

        // Act
        var plan = MailchimpPushPlanner.Plan(list, member, MailingListPushKind.Upsert);

        // Assert
        Assert.Equal(MailchimpPushAction.Upsert, plan.Action);
        Assert.Equal("subscribed", plan.StatusIfNew);
    }

    [Fact]
    public void Plan_UnsubscribedMember_OnUpsert_IsAnUnsubscribe()
    {
        // Arrange
        var list = MailchimpTestContext.List();
        var member = MailchimpTestContext.Member("a@example.com", MailingListMemberStatus.Unsubscribed);

        // Act
        var plan = MailchimpPushPlanner.Plan(list, member, MailingListPushKind.Upsert);

        // Assert
        Assert.Equal(MailchimpPushAction.Unsubscribe, plan.Action);
    }

    [Fact]
    public void Plan_CleanedMember_SendsNothing()
    {
        // Arrange
        var list = MailchimpTestContext.List();
        var member = MailchimpTestContext.Member("a@example.com", MailingListMemberStatus.Cleaned);

        // Act
        var plan = MailchimpPushPlanner.Plan(list, member, MailingListPushKind.Upsert);

        // Assert
        Assert.Equal(MailchimpPushAction.Skip, plan.Action);
    }

    [Theory]
    [InlineData(MailingListPushKind.Unsubscribe, MailchimpPushAction.Unsubscribe)]
    [InlineData(MailingListPushKind.Erase, MailchimpPushAction.Erase)]
    public void Plan_UnsubscribeAndErase_AreSentWhateverTheMemberState(MailingListPushKind kind, MailchimpPushAction expected)
    {
        // Arrange
        var list = MailchimpTestContext.List();
        var member = MailchimpTestContext.Member("a@example.com", MailingListMemberStatus.Cleaned);

        // Act
        var plan = MailchimpPushPlanner.Plan(list, member, kind);

        // Assert
        Assert.Equal(expected, plan.Action);
    }

    [Theory]
    [InlineData(MailingListMemberStatus.Subscribed, "unsubscribed", "pending")]
    [InlineData(MailingListMemberStatus.Subscribed, "cleaned", "pending")]
    [InlineData(MailingListMemberStatus.Subscribed, "archived", "pending")]
    [InlineData(MailingListMemberStatus.Subscribed, "transactional", "subscribed")]
    [InlineData(MailingListMemberStatus.Subscribed, "subscribed", null)]
    [InlineData(MailingListMemberStatus.Subscribed, "pending", null)]
    [InlineData(MailingListMemberStatus.Subscribed, null, null)]
    [InlineData(MailingListMemberStatus.Pending, "unsubscribed", "pending")]
    [InlineData(MailingListMemberStatus.Pending, "cleaned", "pending")]
    [InlineData(MailingListMemberStatus.Pending, "transactional", "pending")]
    [InlineData(MailingListMemberStatus.Pending, "pending", null)]
    [InlineData(MailingListMemberStatus.Pending, "subscribed", null)]
    public void FollowUpStatus_AsksAnAddressMailchimpHoldsAsUnsubscribedToConfirmAgain_AndLeavesPendingAndSubscribedAlone(
        MailingListMemberStatus memberStatus,
        string mailchimpStatus,
        string expected)
    {
        // Arrange
        var plan = MailchimpPushPlanner.Plan(
            MailchimpTestContext.List(MailingListConfirmation.Provider),
            MailchimpTestContext.Member("a@example.com", memberStatus),
            MailingListPushKind.Upsert);

        // Act
        var status = MailchimpPushPlanner.FollowUpStatus(plan, mailchimpStatus);

        // Assert
        Assert.Equal(expected, status);
    }

    [Fact]
    public void FollowUpStatus_OfAPushThatIsNotAnUpsert_IsNothing()
    {
        // Act
        var status = MailchimpPushPlanner.FollowUpStatus(new MailchimpPushPlan { Action = MailchimpPushAction.Unsubscribe }, "unsubscribed");

        // Assert
        Assert.Null(status);
    }

    [Theory]
    [InlineData("subscribed", MailingListMemberStatus.Subscribed)]
    [InlineData("unsubscribed", MailingListMemberStatus.Unsubscribed)]
    [InlineData("archived", MailingListMemberStatus.Unsubscribed)]
    [InlineData("cleaned", MailingListMemberStatus.Cleaned)]
    [InlineData("pending", MailingListMemberStatus.Pending)]
    [InlineData("transactional", null)]
    [InlineData("something-new", null)]
    public void ToMemberStatus_MapsMailchimpStatuses(string mailchimpStatus, MailingListMemberStatus? expected)
    {
        // Act
        var status = MailchimpPushPlanner.ToMemberStatus(mailchimpStatus);

        // Assert
        Assert.Equal(expected, status);
    }
}
