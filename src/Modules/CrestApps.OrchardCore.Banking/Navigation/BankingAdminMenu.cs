using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Banking.Navigation;

/// <summary>
/// Adds the Banking menu: every bank account, and a person's own accounts.
/// </summary>
public sealed class BankingAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BankingAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BankingAdminMenu(IStringLocalizer<BankingAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Banking"], S["Banking"].PrefixPosition("6"), banking => banking
            .AddClass("banking")
            .AddClass("icon-class-fa-solid")
            .AddClass("icon-class-fa-building-columns")
            .Id(BankingConstants.AdminMenuId)
            .Add(S["Bank Accounts"], S["Bank Accounts"].PrefixPosition("10"), accounts => accounts
                .Id("bankAccounts")
                .Action("Index", "BankAccounts", BankingConstants.Features.Banking)
                .Permission(BankingPermissions.ManageBankAccounts)
                .LocalNav())
            .Add(S["My Bank Accounts"], S["My Bank Accounts"].PrefixPosition("90"), mine => mine
                .Id("myBankAccounts")
                .Action("Index", "MyBankAccounts", BankingConstants.Features.Banking)
                .Permission(BankingPermissions.ManageOwnBankAccounts)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
