using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.RoutingRules;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// A routing rule takes only the conversations it names: an enabled rule, for the moment it is tried at, on the
/// channels, numbers, queues and campaigns it lists. An empty list takes every value, so a rule works for any channel.
/// </summary>
public sealed class MessagingRoutingRuleMatcherTests
{
    private static readonly DateTime _now = new(2026, 11, 24, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ARuleWithNoConditions_TakesAConversationOnAnyChannel()
    {
        var rule = Rule();

        Assert.True(MessagingRoutingRuleMatcher.Applies(rule, Request(Conversation(channel: "SMS")), _now));
        Assert.True(MessagingRoutingRuleMatcher.Applies(rule, Request(Conversation(channel: "Email")), _now));
        Assert.True(MessagingRoutingRuleMatcher.Applies(rule, Request(Conversation(channel: "WhatsApp")), _now));
    }

    [Fact]
    public void ADisabledRule_TakesNothing()
    {
        var rule = Rule();
        rule.Enabled = false;

        Assert.False(MessagingRoutingRuleMatcher.Applies(rule, Request(Conversation()), _now));
    }

    [Theory]
    [InlineData(MessagingRoutingRuleTriggers.ContactReplied, MessagingRoutingRuleTriggers.ContactReplied, true)]
    [InlineData(MessagingRoutingRuleTriggers.ContactReplied, MessagingRoutingRuleTriggers.NewConversation, false)]
    [InlineData(MessagingRoutingRuleTriggers.ContactReplied | MessagingRoutingRuleTriggers.NewConversation, MessagingRoutingRuleTriggers.NewConversation, true)]
    [InlineData(MessagingRoutingRuleTriggers.NewConversation, MessagingRoutingRuleTriggers.StartedWithoutAgent, false)]
    [InlineData(MessagingRoutingRuleTriggers.StartedWithoutAgent, MessagingRoutingRuleTriggers.StartedWithoutAgent, true)]
    public void ARule_IsTriedOnlyAtTheMomentsItNames(MessagingRoutingRuleTriggers ruleTriggers, MessagingRoutingRuleTriggers trigger, bool expected)
    {
        var rule = Rule();
        rule.Triggers = ruleTriggers;

        Assert.Equal(expected, MessagingRoutingRuleMatcher.Applies(rule, Request(Conversation(), trigger), _now));
    }

    [Theory]
    [InlineData("SMS", true)]
    [InlineData("sms", true)]
    [InlineData("Email", false)]
    public void AChannelCondition_TakesOnlyItsChannels(string channel, bool expected)
    {
        var rule = Rule();
        rule.Channels = ["SMS"];

        Assert.Equal(expected, MessagingRoutingRuleMatcher.Applies(rule, Request(Conversation(channel: channel)), _now));
    }

    [Theory]
    [InlineData("+17025550100", true)]
    [InlineData("+17025550199", false)]
    public void ANumberCondition_TakesOnlyConversationsOnItsNumbers(string serviceAddress, bool expected)
    {
        var rule = Rule();
        rule.ServiceAddresses = ["+17025550100", "+17025550101"];

        Assert.Equal(expected, MessagingRoutingRuleMatcher.Applies(rule, Request(Conversation(serviceAddress: serviceAddress)), _now));
    }

    [Theory]
    [InlineData("sales", true)]
    [InlineData("support", false)]
    [InlineData(null, false)]
    public void AQueueCondition_TakesOnlyConversationsOfItsQueues(string queueId, bool expected)
    {
        var rule = Rule();
        rule.QueueIds = ["sales"];

        Assert.Equal(expected, MessagingRoutingRuleMatcher.Applies(rule, Request(Conversation(), queueId: queueId), _now));
    }

    [Theory]
    [InlineData("campaign-1", true)]
    [InlineData("campaign-2", false)]
    [InlineData(null, false)]
    public void ACampaignCondition_TakesOnlyConversationsOfItsCampaigns(string campaignId, bool expected)
    {
        var rule = Rule();
        rule.CampaignIds = ["campaign-1"];

        var conversation = Conversation();
        conversation.CampaignId = campaignId;

        Assert.Equal(expected, MessagingRoutingRuleMatcher.Applies(rule, Request(conversation), _now));
    }

    [Fact]
    public void BlankEntriesInACondition_CountAsNoCondition()
    {
        var rule = Rule();
        rule.Channels = [" ", string.Empty];

        Assert.True(MessagingRoutingRuleMatcher.Applies(rule, Request(Conversation(channel: "Email")), _now));
    }

    [Fact]
    public void EveryCondition_MustHold()
    {
        var rule = Rule();
        rule.Channels = ["SMS"];
        rule.QueueIds = ["sales"];

        Assert.True(MessagingRoutingRuleMatcher.Applies(rule, Request(Conversation(channel: "SMS"), queueId: "sales"), _now));
        Assert.False(MessagingRoutingRuleMatcher.Applies(rule, Request(Conversation(channel: "SMS"), queueId: "support"), _now));
        Assert.False(MessagingRoutingRuleMatcher.Applies(rule, Request(Conversation(channel: "Email"), queueId: "sales"), _now));
    }

    [Theory]
    [InlineData(10, 9, false)]
    [InlineData(10, 10, true)]
    [InlineData(10, 30, true)]
    [InlineData(0, 1, true)]
    public void AWaitingRule_TakesAConversationOnlyAfterItsWait(int waitingMinutes, int minutesSinceLastMessage, bool expected)
    {
        var rule = Rule();
        rule.Triggers = MessagingRoutingRuleTriggers.Waiting;
        rule.WaitingMinutes = waitingMinutes;

        var conversation = Conversation();
        conversation.LastMessageUtc = _now.AddMinutes(-minutesSinceLastMessage);

        Assert.Equal(expected, MessagingRoutingRuleMatcher.Applies(rule, Request(conversation, MessagingRoutingRuleTriggers.Waiting), _now));
    }

    [Fact]
    public void AWaitingRule_SkipsAConversationWithNoMessage()
    {
        var rule = Rule();
        rule.Triggers = MessagingRoutingRuleTriggers.Waiting;
        rule.WaitingMinutes = 5;

        Assert.False(MessagingRoutingRuleMatcher.Applies(rule, Request(Conversation(), MessagingRoutingRuleTriggers.Waiting), _now));
    }

    private static MessagingRoutingRule Rule()
        => new()
        {
            ItemId = "rule-1",
            DisplayText = "Rule",
            Enabled = true,
            Triggers = MessagingRoutingRuleTriggers.ContactReplied,
            CandidateSource = MessagingRoutingCandidateSources.Users,
            UserIds = ["user-1"],
        };

    private static MessagingConversation Conversation(string channel = "SMS", string serviceAddress = "+17025550100")
        => new()
        {
            ItemId = "conversation-1",
            Channel = channel,
            ServiceAddress = serviceAddress,
            ContactAddress = "+17025550142",
        };

    private static MessagingRoutingRuleRequest Request(
        MessagingConversation conversation,
        MessagingRoutingRuleTriggers trigger = MessagingRoutingRuleTriggers.ContactReplied,
        string queueId = null)
        => new()
        {
            Conversation = conversation,
            Trigger = trigger,
            QueueId = queueId,
        };
}
