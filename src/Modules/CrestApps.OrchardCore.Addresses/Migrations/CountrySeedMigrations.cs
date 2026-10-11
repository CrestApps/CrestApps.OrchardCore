using CrestApps.OrchardCore.Addresses;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Data.Migration;
using OrchardCore.Title.Models;
using YesSql;

namespace CrestApps.OrchardCore.Addresses.Migrations;

/// <summary>
/// Seeds the canonical ISO 3166-1 country list as editable <c>Country</c> content items so administrators can
/// manage countries, regions, and cities through the standard content management experience.
/// </summary>
public sealed class CountrySeedMigrations : DataMigration
{
    private readonly IContentManager _contentManager;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="CountrySeedMigrations"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager used to create and publish country content items.</param>
    /// <param name="session">The YesSql session used to detect already-seeded countries.</param>
    public CountrySeedMigrations(
        IContentManager contentManager,
        ISession session)
    {
        _contentManager = contentManager;
        _session = session;
    }

    /// <summary>
    /// Creates and publishes a published country content item for every canonical ISO country that is not
    /// already present.
    /// </summary>
    /// <returns>The next migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        var existing = await _session.Query<ContentItem, ContentItemIndex>(index =>
                index.ContentType == AddressConstants.Country && index.Latest)
            .ListAsync();

        var seededCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in existing)
        {
            var code = item.Content?[AddressConstants.CountryPart]?["Code"]?["Text"]?.GetValue<string>();

            if (!string.IsNullOrWhiteSpace(code))
            {
                seededCodes.Add(code.Trim());
            }
        }

        foreach (var country in CountryProvider.GetCountries())
        {
            if (seededCodes.Contains(country.Code))
            {
                continue;
            }

            var contentItem = await _contentManager.NewAsync(AddressConstants.Country);

            contentItem.DisplayText = country.Name;
            contentItem.Alter<TitlePart>(part => part.Title = country.Name);
            contentItem.Content.CountryPart.Code.Text = country.Code;

            await _contentManager.CreateAsync(contentItem, VersionOptions.Published);
        }

        return 1;
    }
}
