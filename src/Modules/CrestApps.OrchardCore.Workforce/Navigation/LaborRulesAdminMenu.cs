using Microsoft.Extensions.Localization;
using OrchardCore.Environment.Shell;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.Navigation;

/// <summary>
/// Adds the Labor Rules and Rule Overrides items to the Workforce menu, and Predictability Pay beside timesheets under
/// Time Tracker when the time clock is on (under Workforce otherwise).
/// </summary>
public sealed class LaborRulesAdminMenu : AdminNavigationProvider
{
    private readonly IShellFeaturesManager _shellFeaturesManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LaborRulesAdminMenu"/> class.
    /// </summary>
    /// <param name="shellFeaturesManager">The shell features manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LaborRulesAdminMenu(IShellFeaturesManager shellFeaturesManager, IStringLocalizer<LaborRulesAdminMenu> stringLocalizer)
    {
        _shellFeaturesManager = shellFeaturesManager;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        var timeClock = (await _shellFeaturesManager.GetEnabledFeaturesAsync()).Any(feature => feature.Id == WorkforceConstants.Features.TimeClock);

        builder.Add(timeClock ? S["Time Tracker"] : S["Workforce"], parent => parent
            .Add(S["Predictability Pay"], S["Predictability Pay"].PrefixPosition(timeClock ? "33" : "24"), pay => pay
                .Id("workforcePredictabilityPay")
                .Action("Index", "PredictabilityPay", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ApprovePredictabilityPay)
                .LocalNav()));

        builder.Add(S["Workforce"], workforce => workforce
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
    }
}
