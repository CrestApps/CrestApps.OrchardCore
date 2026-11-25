using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Handlers;

/// <summary>
/// Stamps messaging routing rules, binds the values a recipe or deployment plan gives, and checks what a rule needs to
/// work, so the editor, a recipe and a deployment plan all keep the same rules.
/// </summary>
internal sealed class MessagingRoutingRuleHandler : CatalogEntryHandlerBase<MessagingRoutingRule>
{
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingRoutingRuleHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public MessagingRoutingRuleHandler(
        IClock clock,
        IStringLocalizer<MessagingRoutingRuleHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task InitializingAsync(InitializingContext<MessagingRoutingRule> context, CancellationToken cancellationToken = default)
    {
        // A recipe or deployment plan hands the entry's JSON to the manager; the editor hands none.
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);

        if (context.Model.CreatedUtc == default)
        {
            context.Model.CreatedUtc = _clock.UtcNow;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task UpdatingAsync(UpdatingContext<MessagingRoutingRule> context, CancellationToken cancellationToken = default)
    {
        CatalogDeploymentSerializer.Populate(context.Model, context.Data);

        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override Task ValidatingAsync(ValidatingContext<MessagingRoutingRule> context, CancellationToken cancellationToken = default)
    {
        var rule = context.Model;

        if (string.IsNullOrWhiteSpace(rule.DisplayText))
        {
            context.Result.Fail(new ValidationResult(S["A name is required."], [nameof(MessagingRoutingRule.DisplayText)]));
        }

        if (rule.Triggers == MessagingRoutingRuleTriggers.None)
        {
            context.Result.Fail(new ValidationResult(S["Choose at least one moment the rule applies at."], [nameof(MessagingRoutingRule.Triggers)]));
        }

        if (rule.Triggers.HasFlag(MessagingRoutingRuleTriggers.Waiting) && rule.WaitingMinutes < 1)
        {
            context.Result.Fail(new ValidationResult(S["The wait must be at least one minute."], [nameof(MessagingRoutingRule.WaitingMinutes)]));
        }

        switch (rule.CandidateSource)
        {
            case MessagingRoutingCandidateSources.QueueSignedIn:
            case MessagingRoutingCandidateSources.QueueMembers:
                break;

            case MessagingRoutingCandidateSources.Teams:
                if (!HasAny(rule.TeamIds))
                {
                    context.Result.Fail(new ValidationResult(S["Choose at least one team."], [nameof(MessagingRoutingRule.TeamIds)]));
                }

                break;

            case MessagingRoutingCandidateSources.Roles:
                if (!HasAny(rule.RoleNames))
                {
                    context.Result.Fail(new ValidationResult(S["Choose at least one role."], [nameof(MessagingRoutingRule.RoleNames)]));
                }

                break;

            case MessagingRoutingCandidateSources.Users:
                if (!HasAny(rule.UserIds))
                {
                    context.Result.Fail(new ValidationResult(S["Choose at least one user."], [nameof(MessagingRoutingRule.UserIds)]));
                }

                break;

            default:
                context.Result.Fail(new ValidationResult(S["Choose who can get the conversations."], [nameof(MessagingRoutingRule.CandidateSource)]));
                break;
        }

        if (rule.Strategy is not MessagingRoutingRuleStrategies.RoundRobin and
            not MessagingRoutingRuleStrategies.LeastOpenConversations and
            not MessagingRoutingRuleStrategies.LeastLoaded)
        {
            context.Result.Fail(new ValidationResult(S["Choose how an agent is picked."], [nameof(MessagingRoutingRule.Strategy)]));
        }

        return Task.CompletedTask;
    }

    private static bool HasAny(IList<string> values)
        => values is not null && values.Any(value => !string.IsNullOrWhiteSpace(value));
}
