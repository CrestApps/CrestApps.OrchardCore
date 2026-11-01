using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.Navigation;

/// <summary>
/// Adds the Labor Rules, Predictability Pay and Rule Overrides items to the Workforce menu.
/// </summary>
public sealed class LaborRulesAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LaborRulesAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LaborRulesAdminMenu(IStringLocalizer<LaborRulesAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Workforce"], workforce => workforce
            .Add(S["Predictability Pay"], S["Predictability Pay"].PrefixPosition("24"), pay => pay
                .Id("workforcePredictabilityPay")
                .Action("Index", "PredictabilityPay", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ApprovePredictabilityPay)
                .LocalNav())
            .Add(S["Labor Rules"], S["Labor Rules"].PrefixPosition("85"), rules => rules
                .Id("workforceLaborRules")
                .Action("Index", "LaborRuleSets", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ManageLaborRules)
                .LocalNav())
            .Add(S["Rule Overrides"], S["Rule Overrides"].PrefixPosition("86"), overrides => overrides
                .Id("workforceRuleOverrides")
                .Action("Index", "LaborRuleOverrides", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ManageLaborRules)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
