using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Handlers;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// A routing rule is stored only when it can work: it has a name, a moment it is tried at, someone to give
/// conversations to, and a way to pick one. The editor, a recipe and a deployment plan all go through these checks.
/// </summary>
public sealed class MessagingRoutingRuleHandlerTests
{
    [Fact]
    public async Task AWorkingRule_IsValid()
    {
        var errors = await ValidateAsync(Rule());

        Assert.Empty(errors);
    }

    [Fact]
    public async Task ARuleWithoutAName_IsRefused()
    {
        var rule = Rule();
        rule.DisplayText = " ";

        Assert.Contains(nameof(MessagingRoutingRule.DisplayText), await ValidateAsync(rule));
    }

    [Fact]
    public async Task ARuleNeverTried_IsRefused()
    {
        var rule = Rule();
        rule.Triggers = MessagingRoutingRuleTriggers.None;

        Assert.Contains(nameof(MessagingRoutingRule.Triggers), await ValidateAsync(rule));
    }

    [Fact]
    public async Task AWaitingRuleWithoutAWait_IsRefused()
    {
        var rule = Rule();
        rule.Triggers = MessagingRoutingRuleTriggers.Waiting;
        rule.WaitingMinutes = 0;

        Assert.Contains(nameof(MessagingRoutingRule.WaitingMinutes), await ValidateAsync(rule));
    }

    [Theory]
    [InlineData(MessagingRoutingCandidateSources.Teams, nameof(MessagingRoutingRule.TeamIds))]
    [InlineData(MessagingRoutingCandidateSources.Roles, nameof(MessagingRoutingRule.RoleNames))]
    [InlineData(MessagingRoutingCandidateSources.Users, nameof(MessagingRoutingRule.UserIds))]
    public async Task ASourceWithNothingToRead_IsRefused(string source, string member)
    {
        var rule = Rule();
        rule.CandidateSource = source;
        rule.TeamIds = [];
        rule.RoleNames = [];
        rule.UserIds = [" "];

        Assert.Contains(member, await ValidateAsync(rule));
    }

    [Theory]
    [InlineData(MessagingRoutingCandidateSources.QueueSignedIn)]
    [InlineData(MessagingRoutingCandidateSources.QueueMembers)]
    public async Task AQueueSource_NeedsNoListBecauseItCanUseTheConversationsQueue(string source)
    {
        var rule = Rule();
        rule.CandidateSource = source;
        rule.UserIds = [];

        Assert.Empty(await ValidateAsync(rule));
    }

    [Fact]
    public async Task AnUnknownSourceOrStrategy_IsRefused()
    {
        var rule = Rule();
        rule.CandidateSource = "Somebody";
        rule.Strategy = "Coin flip";

        var errors = await ValidateAsync(rule);

        Assert.Contains(nameof(MessagingRoutingRule.CandidateSource), errors);
        Assert.Contains(nameof(MessagingRoutingRule.Strategy), errors);
    }

    private static async Task<IReadOnlyList<string>> ValidateAsync(MessagingRoutingRule rule)
    {
        var handler = new MessagingRoutingRuleHandler(Mock.Of<IClock>(), new PassThroughStringLocalizer<MessagingRoutingRuleHandler>());
        var context = new ValidatingContext<MessagingRoutingRule>(rule);

        await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);

        return context.Result.Errors.SelectMany(error => error.MemberNames).ToList();
    }

    private static MessagingRoutingRule Rule()
        => new()
        {
            ItemId = "rule-1",
            DisplayText = "Workflow texts to the sales team",
            Enabled = true,
            Triggers = MessagingRoutingRuleTriggers.ContactReplied,
            CandidateSource = MessagingRoutingCandidateSources.Users,
            UserIds = ["user-1"],
            Strategy = MessagingRoutingRuleStrategies.RoundRobin,
        };
}
