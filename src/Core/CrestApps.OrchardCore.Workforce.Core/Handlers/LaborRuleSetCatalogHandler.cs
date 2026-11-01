using System.ComponentModel.DataAnnotations;
using CrestApps.Core;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Handlers;

/// <summary>
/// Stamps and checks labor rule sets: a name and rules that are in the catalog, each with an id and the rule's
/// parameters.
/// </summary>
internal sealed class LaborRuleSetCatalogHandler : CatalogEntryHandlerBase<LaborRuleSet>
{
    private readonly IEnumerable<ILaborRule> _rules;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public LaborRuleSetCatalogHandler(IEnumerable<ILaborRule> rules, IClock clock, IStringLocalizer<LaborRuleSetCatalogHandler> stringLocalizer)
    {
        _rules = rules;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializedAsync(InitializedContext<LaborRuleSet> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override Task UpdatingAsync(UpdatingContext<LaborRuleSet> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    public override Task ValidatingAsync(ValidatingContext<LaborRuleSet> context, CancellationToken cancellationToken = default)
    {
        var set = context.Model;

        set.Name = set.Name?.Trim();

        if (string.IsNullOrEmpty(set.Name))
        {
            context.Result.Fail(new ValidationResult(S["The name is required."], [nameof(LaborRuleSet.Name)]));
        }

        var known = _rules.ToDictionary(rule => rule.Key, StringComparer.Ordinal);

        foreach (var instance in set.Rules)
        {
            if (string.IsNullOrEmpty(instance.Id))
            {
                instance.Id = UniqueId.GenerateId();
            }

            if (string.IsNullOrEmpty(instance.RuleKey) || !known.TryGetValue(instance.RuleKey, out var rule))
            {
                context.Result.Fail(new ValidationResult(S["The rule '{0}' isn't in the catalog.", instance.RuleKey ?? string.Empty], [nameof(LaborRuleSet.Rules)]));

                continue;
            }

            foreach (var parameter in rule.Parameters)
            {
                instance.Parameters.TryAdd(parameter.Name, parameter.DefaultValue);
            }
        }

        if (set.Rules.GroupBy(instance => instance.Id, StringComparer.Ordinal).Any(group => group.Count() > 1))
        {
            context.Result.Fail(new ValidationResult(S["Each rule in a set needs its own id."], [nameof(LaborRuleSet.Rules)]));
        }

        return Task.CompletedTask;
    }
}
