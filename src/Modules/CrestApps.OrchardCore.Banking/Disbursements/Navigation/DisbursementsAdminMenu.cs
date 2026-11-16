using CrestApps.OrchardCore.Banking;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Banking.Disbursements.Navigation;

/// <summary>
/// Adds Disbursements and Disbursement Batches under Banking.
/// </summary>
public sealed class DisbursementsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisbursementsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DisbursementsAdminMenu(IStringLocalizer<DisbursementsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Banking"], S["Banking"].PrefixPosition("6"), banking => banking
            .Id(BankingConstants.AdminMenuId)
            .Add(S["Disbursements"], S["Disbursements"].PrefixPosition("40"), disbursements => disbursements
                .Id("disbursements")
                .Action("Index", "Disbursements", DisbursementsConstants.Features.Disbursements)
                .Permission(DisbursementsPermissions.ViewDisbursements)
                .LocalNav())
            .Add(S["Disbursement Batches"], S["Disbursement Batches"].PrefixPosition("50"), batches => batches
                .Id("disbursementBatches")
                .Action("Index", "DisbursementBatches", DisbursementsConstants.Features.Disbursements)
                .Permission(DisbursementsPermissions.ManageDisbursements)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
