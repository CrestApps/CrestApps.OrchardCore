using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Commerce.Navigation;

/// <summary>
/// Registers the shared top-level Commerce admin menu node so every commerce-related module can contribute
/// its screens under a single, consistently branded and iconed menu.
/// </summary>
public sealed class CommerceAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommerceAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CommerceAdminMenu(IStringLocalizer<CommerceAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], "15", commerce => commerce
                .AddClass("commerce")
                .Id("commerce")
            );

        return ValueTask.CompletedTask;
    }
}
