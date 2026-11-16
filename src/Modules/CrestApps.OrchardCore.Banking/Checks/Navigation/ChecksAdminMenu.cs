using CrestApps.OrchardCore.Banking;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Banking.Checks.Navigation;

/// <summary>
/// Adds the check register and the check layouts to the Banking menu.
/// </summary>
public sealed class ChecksAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChecksAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ChecksAdminMenu(IStringLocalizer<ChecksAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Banking"], S["Banking"].PrefixPosition("6"), banking => banking
            .Id(BankingConstants.AdminMenuId)
            .Add(S["Checks"], S["Checks"].PrefixPosition("20"), checks => checks
                .Id("checks")
                .Action("Index", "Checks", BankingConstants.Features.Banking)
                .Permission(ChecksPermissions.ViewCheckRegister)
                .LocalNav())
            .Add(S["Check Layouts"], S["Check Layouts"].PrefixPosition("30"), layouts => layouts
                .Id("checkLayouts")
                .Action("Index", "CheckLayouts", BankingConstants.Features.Banking)
                .Permission(ChecksPermissions.ManageCheckLayouts)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
