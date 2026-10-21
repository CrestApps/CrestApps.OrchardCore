using CrestApps.OrchardCore.Addresses;
using OrchardCore.ContentManagement;
using OrchardCore.Data.Migration;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Addresses.Migrations;

/// <summary>
/// Seeds the canonical ISO 3166-1 country list as editable <c>Country</c> content items so administrators can
/// manage the countries the site serves through the standard content management experience.
/// </summary>
internal sealed class CountrySeedMigrations : DataMigration
{
    private readonly IContentManager _contentManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="CountrySeedMigrations"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager used to create and publish country content items.</param>
    public CountrySeedMigrations(IContentManager contentManager)
    {
        _contentManager = contentManager;
    }

    /// <summary>
    /// Creates and publishes a country content item for every canonical ISO country.
    /// </summary>
    /// <returns>The next migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        foreach (var country in CountryProvider.GetCountries())
        {
            var contentItem = await _contentManager.NewAsync(AddressConstants.Country);

            contentItem.DisplayText = country.Name;
            contentItem.Alter<TitlePart>(part => part.Title = country.Name);
            contentItem.Content.CountryPart.Code.Text = country.Code;

            await _contentManager.CreateAsync(contentItem, VersionOptions.Published);
        }

        return 1;
    }
}
