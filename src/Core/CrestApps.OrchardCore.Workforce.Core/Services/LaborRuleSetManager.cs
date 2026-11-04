using CrestApps.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="ILaborRuleSetManager"/>.
/// </summary>
internal sealed class LaborRuleSetManager : CatalogManager<LaborRuleSet>, ILaborRuleSetManager
{
    private readonly IEnumerable<ILaborRule> _rules;

    public LaborRuleSetManager(
        LaborRuleSetStore store,
        IEnumerable<ILaborRule> rules,
        IEnumerable<ICatalogEntryHandler<LaborRuleSet>> handlers,
        ILogger<CatalogManager<LaborRuleSet>> logger)
        : base(store, handlers, logger)
    {
        _rules = rules;
    }

    public IReadOnlyList<ILaborRule> ListRules()
    {
        return _rules.OrderBy(rule => rule.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public LaborRuleInstance NewRule(string ruleKey)
    {
        var rule = _rules.FirstOrDefault(item => item.Key == ruleKey);

        if (rule is null)
        {
            return null;
        }

        var instance = new LaborRuleInstance
        {
            Id = CrestApps.Core.UniqueId.GenerateId(),
            RuleKey = rule.Key,
            Enforcement = LaborRuleEnforcement.Warn,
        };

        foreach (var parameter in rule.Parameters)
        {
            instance.Parameters[parameter.Name] = parameter.DefaultValue;
        }

        return instance;
    }
}
