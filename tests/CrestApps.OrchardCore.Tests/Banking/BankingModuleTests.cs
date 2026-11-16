using System.Reflection;
using CrestApps.OrchardCore.Banking;
using CrestApps.OrchardCore.Banking.Checks;
using CrestApps.OrchardCore.Banking.Checks.Navigation;
using CrestApps.OrchardCore.Banking.Disbursements;
using CrestApps.OrchardCore.Banking.Disbursements.Navigation;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using OrchardCore.Modules.Manifest;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Tests.Banking;

/// <summary>
/// Banking is one module with four features: its pages are one MVC area, and Payroll owns no banking feature.
/// </summary>
public sealed class BankingModuleTests
{
    [Fact]
    public async Task EveryBankingMenuItem_RoutesToTheBankingModule()
    {
        var builder = new NavigationBuilder();

        await new ChecksAdminMenu(new PassThroughStringLocalizer<ChecksAdminMenu>()).BuildNavigationAsync(NavigationConstants.AdminId, builder);
        await new DisbursementsAdminMenu(new PassThroughStringLocalizer<DisbursementsAdminMenu>()).BuildNavigationAsync(NavigationConstants.AdminId, builder);

        var routed = Flatten(builder.Build()).Where(item => item.RouteValues?.ContainsKey("area") == true).ToList();

        // The area is the module's name: a feature id (CrestApps.OrchardCore.Banking.Checks) is no area.
        Assert.Equal(4, routed.Count);
        Assert.All(routed, item => Assert.Equal("CrestApps.OrchardCore.Banking", item.RouteValues["area"]));
    }

    [Fact]
    public void BankingModule_HasFourFinanceFeatures_AndPayrollNone()
    {
        var banking = typeof(global::CrestApps.OrchardCore.Banking.Startup).Assembly.GetCustomAttributes<FeatureAttribute>().Where(feature => feature.GetType() == typeof(FeatureAttribute)).ToDictionary(feature => feature.Id);

        Assert.Equal(
            [(BankingConstants.Features.Banking, "Bank Accounts"), (ChecksConstants.Features.Checks, "Checks"), (DisbursementsConstants.Features.Disbursements, "Disbursements"), (DisbursementsConstants.Features.DirectDeposit, "Direct Deposit")],
            banking.Values.Select(feature => (feature.Id, feature.Name)));
        Assert.All(banking.Values, feature => Assert.Equal("Finance", feature.Category));
        Assert.Equal(["CrestApps.OrchardCore.Banking.Checks", "CrestApps.OrchardCore.Banking.Disbursements", "CrestApps.OrchardCore.Banking.DirectDeposit"], [ChecksConstants.Features.Checks, DisbursementsConstants.Features.Disbursements, DisbursementsConstants.Features.DirectDeposit]);

        var payroll = typeof(global::CrestApps.OrchardCore.Payroll.PaySetupStartup).Assembly.GetCustomAttributes<FeatureAttribute>().Select(feature => feature.Id).ToList();

        Assert.DoesNotContain("CrestApps.OrchardCore.Payroll.DirectDeposit", payroll);
        Assert.DoesNotContain("CrestApps.OrchardCore.Payroll.Checks", payroll);
    }

    private static IEnumerable<MenuItem> Flatten(IEnumerable<MenuItem> items)
        => items.SelectMany(item => new[] { item }.Concat(Flatten(item.Items ?? [])));
}
