using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.PageSections;

/// <summary>
/// A section of a record's page, such as the Teams section of a staff profile or the Orders section of an account: a
/// name in the page's side menu, with the shapes placed in it on the right.
/// </summary>
public sealed class PageSection
{
    /// <summary>
    /// Gets or sets the section's name, which shapes use in their placement (see <see cref="PageSectionPlacement.Location"/>).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the title shown in the side menu.
    /// </summary>
    public LocalizationSource Title { get; set; }

    /// <summary>
    /// Gets or sets the Font Awesome classes of the section's icon, such as <c>fa-solid fa-people-group</c>.
    /// </summary>
    public string Icon { get; set; }

    /// <summary>
    /// Gets or sets the section's position in the side menu; lower comes first.
    /// </summary>
    public double Position { get; set; }
}

/// <summary>
/// The sections registered for each page, by page name.
/// </summary>
public sealed class PageSectionsOptions
{
    /// <summary>
    /// Gets the sections of each page, by page name, then by section name.
    /// </summary>
    public Dictionary<string, Dictionary<string, PageSection>> Pages { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Finds a registered section of a page.
    /// </summary>
    /// <param name="page">The page name.</param>
    /// <param name="name">The section name.</param>
    /// <returns>The section, or <see langword="null"/> when it isn't registered.</returns>
    public PageSection Find(string page, string name)
        => !string.IsNullOrEmpty(page) && !string.IsNullOrEmpty(name) && Pages.TryGetValue(page, out var sections) && sections.TryGetValue(name, out var section)
            ? section
            : null;
}

/// <summary>
/// Places shapes in a section of a record's page.
/// </summary>
public static class PageSectionPlacement
{
    /// <summary>
    /// Gets the placement of a shape in a section of a page: the <c>Content</c> zone, grouped as the section's tab. Use
    /// it in a display driver's <c>Location</c> for the page's display type.
    /// </summary>
    /// <param name="section">The section name.</param>
    /// <param name="position">The shape's position inside the section.</param>
    /// <returns>The placement, such as <c>Content:2#Teams</c>.</returns>
    public static string Location(string section, string position = "1")
        => $"Content:{position}#{section}";
}

/// <summary>
/// Registers the sections of record pages.
/// </summary>
public static class PageSectionsServiceCollectionExtensions
{
    /// <summary>
    /// Registers a section of a page, so the page's side menu shows it with its title and icon, in its position. The
    /// module that adds shapes to the section registers it; registering it again replaces it.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="page">The page name, such as <c>Staff</c> or <c>Account</c>.</param>
    /// <param name="name">The section name.</param>
    /// <param name="title">The title shown in the side menu.</param>
    /// <param name="icon">The Font Awesome classes of the icon.</param>
    /// <param name="position">The position in the side menu.</param>
    /// <returns>The services.</returns>
    public static IServiceCollection AddPageSection(this IServiceCollection services, string page, string name, LocalizationSource title, string icon, double position)
    {
        ArgumentException.ThrowIfNullOrEmpty(page);
        ArgumentException.ThrowIfNullOrEmpty(name);

        services.Configure<PageSectionsOptions>(options =>
        {
            if (!options.Pages.TryGetValue(page, out var sections))
            {
                options.Pages[page] = sections = new Dictionary<string, PageSection>(StringComparer.OrdinalIgnoreCase);
            }

            sections[name] = new PageSection
            {
                Name = name,
                Title = title,
                Icon = icon,
                Position = position,
            };
        });

        return services;
    }
}
