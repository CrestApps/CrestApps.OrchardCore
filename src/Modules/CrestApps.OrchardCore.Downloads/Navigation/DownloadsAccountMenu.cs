using CrestApps.OrchardCore.Commerce;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Downloads.Navigation;

/// <summary>
/// Adds the customer's downloads to the storefront account.
/// </summary>
public sealed class DownloadsAccountMenu : INavigationProvider
{
    internal readonly IStringLocalizer S;

    public DownloadsAccountMenu(IStringLocalizer<DownloadsAccountMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public ValueTask BuildNavigationAsync(string name, NavigationBuilder builder)
    {
        if (!string.Equals(name, CommerceConstants.Account.MenuName, StringComparison.Ordinal))
        {
            return ValueTask.CompletedTask;
        }

        builder.Add(S["Downloads"], "20", downloads => downloads
            .Id("account-downloads")
            .AddClass("fa-solid fa-download")
            .Action("Index", "AccountDownloads", new { area = DownloadsConstants.Feature }));

        return ValueTask.CompletedTask;
    }
}
