using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Commerce &gt; Taxation, the one place of every tax screen. The Commerce node has the id, position and icon the
/// Commerce module gives it, so the two merge when Commerce is on, and a site with Payroll and no store still gets a
/// proper top-level item.
/// </summary>
internal static class TaxationMenu
{
    /// <summary>
    /// The Commerce node's id.
    /// </summary>
    public const string CommerceId = "commerce";

    /// <summary>
    /// The Commerce node's position, the Commerce module's.
    /// </summary>
    public const string CommercePosition = "15";

    /// <summary>
    /// Adds items under Commerce &gt; Taxation.
    /// </summary>
    /// <param name="builder">The admin menu.</param>
    /// <param name="S">The string localizer.</param>
    /// <param name="items">Adds the items.</param>
    /// <returns>The admin menu.</returns>
    public static NavigationBuilder AddTaxation(this NavigationBuilder builder, IStringLocalizer S, Action<NavigationItemBuilder> items)
        => builder.Add(S["Commerce"], CommercePosition, commerce => commerce
            .Id(CommerceId)
            .AddClass(CommerceId)
            .AddClass("icon-class-fa-solid")
            .AddClass("icon-class-fa-store")
            .Add(S["Taxation"], S["Taxation"].PrefixPosition(), taxation =>
            {
                taxation.Id("taxation");
                items(taxation);
            }));
}
