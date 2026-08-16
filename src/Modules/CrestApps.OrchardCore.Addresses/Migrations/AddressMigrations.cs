using CrestApps.OrchardCore.Addresses;
using OrchardCore.ContentFields.Settings;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Addresses.Migrations;

/// <summary>
/// Creates the full geographic content-type hierarchy (country, region, county, city, and district) together
/// with their information parts and the reusable, attachable address part. Every geographic component of an
/// address, except the postal code, is modeled as a managed content type so it can be reused across checkout,
/// taxation, and subscriptions.
/// </summary>
public sealed class AddressMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AddressMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager used to define the content types and parts.</param>
    public AddressMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Defines the country, region, county, city, and district content types, their parts, and the reusable
    /// address part.
    /// </summary>
    /// <returns>The next migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await DefineCountryAsync();
        await DefineRegionAsync();
        await DefineCountyAsync();
        await DefineCityAsync();
        await DefineDistrictAsync();
        await DefineAddressPartAsync();

        return 2;
    }

    /// <summary>
    /// Upgrades tenants created before the geographic hierarchy was completed. It adds the county and district
    /// content types, standardizes every geographic part on a money-safe <c>Code</c> field, and migrates the
    /// reusable address part from a free-text city to the full set of content-item selectors.
    /// </summary>
    /// <returns>The next migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(AddressConstants.RegionPart, part => part
            .RemoveField("Abbreviation")
        );

        await _contentDefinitionManager.AlterPartDefinitionAsync(AddressConstants.AddressPart, part => part
            .RemoveField("City")
        );

        await DefineCountryAsync();
        await DefineRegionAsync();
        await DefineCountyAsync();
        await DefineCityAsync();
        await DefineDistrictAsync();
        await DefineAddressPartAsync();

        return 2;
    }

    private Task DefineCountryAsync()
    {
        return DefineGeographicTypeAsync(
            AddressConstants.Country,
            AddressConstants.CountryPart,
            "Country",
            "Country name",
            part => part
                .WithDisplayName("Country")
                .WithDescription("Provides the ISO information for a country.")
                .WithField(AddressConstants.CodeField, field => field
                    .OfType("TextField")
                    .WithDisplayName("ISO code")
                    .WithDescription("The ISO 3166-1 alpha-2 country code, for example US or CA.")
                    .WithPosition("1")
                    .WithSettings(new TextFieldSettings
                    {
                        Required = true,
                    })
                ));
    }

    private Task DefineRegionAsync()
    {
        return DefineGeographicTypeAsync(
            AddressConstants.Region,
            AddressConstants.RegionPart,
            "Region",
            "Region name",
            part => part
                .WithDisplayName("Region")
                .WithDescription("Provides the parent country and code for a state, province, or region.")
                .WithField("Country", field => field
                    .OfType("ContentPickerField")
                    .WithDisplayName("Country")
                    .WithPosition("1")
                    .WithSettings(new ContentPickerFieldSettings
                    {
                        Required = true,
                        Multiple = false,
                        DisplayedContentTypes = [AddressConstants.Country],
                    })
                )
                .WithField(AddressConstants.CodeField, field => field
                    .OfType("TextField")
                    .WithDisplayName("Code")
                    .WithDescription("The subdivision code or abbreviation, for example CA for California.")
                    .WithPosition("2")
                ));
    }

    private Task DefineCountyAsync()
    {
        return DefineGeographicTypeAsync(
            AddressConstants.County,
            AddressConstants.CountyPart,
            "County",
            "County name",
            part => part
                .WithDisplayName("County")
                .WithDescription("Provides the parent region and code for a county.")
                .WithField("Region", field => field
                    .OfType("ContentPickerField")
                    .WithDisplayName("Region")
                    .WithPosition("1")
                    .WithSettings(new ContentPickerFieldSettings
                    {
                        Required = true,
                        Multiple = false,
                        DisplayedContentTypes = [AddressConstants.Region],
                    })
                )
                .WithField(AddressConstants.CodeField, field => field
                    .OfType("TextField")
                    .WithDisplayName("Code")
                    .WithDescription("An optional county code used for money-safe matching.")
                    .WithPosition("2")
                ));
    }

    private Task DefineCityAsync()
    {
        return DefineGeographicTypeAsync(
            AddressConstants.City,
            AddressConstants.CityPart,
            "City",
            "City name",
            part => part
                .WithDisplayName("City")
                .WithDescription("Provides the parent region, optional county, and code for a city.")
                .WithField("Region", field => field
                    .OfType("ContentPickerField")
                    .WithDisplayName("Region")
                    .WithPosition("1")
                    .WithSettings(new ContentPickerFieldSettings
                    {
                        Required = true,
                        Multiple = false,
                        DisplayedContentTypes = [AddressConstants.Region],
                    })
                )
                .WithField("County", field => field
                    .OfType("ContentPickerField")
                    .WithDisplayName("County")
                    .WithPosition("2")
                    .WithSettings(new ContentPickerFieldSettings
                    {
                        Multiple = false,
                        DisplayedContentTypes = [AddressConstants.County],
                    })
                )
                .WithField(AddressConstants.CodeField, field => field
                    .OfType("TextField")
                    .WithDisplayName("Code")
                    .WithDescription("An optional city code used for money-safe matching.")
                    .WithPosition("3")
                ));
    }

    private Task DefineDistrictAsync()
    {
        return DefineGeographicTypeAsync(
            AddressConstants.District,
            AddressConstants.DistrictPart,
            "District",
            "District name",
            part => part
                .WithDisplayName("District")
                .WithDescription("Provides the parent city and code for a special or tax district.")
                .WithField("City", field => field
                    .OfType("ContentPickerField")
                    .WithDisplayName("City")
                    .WithPosition("1")
                    .WithSettings(new ContentPickerFieldSettings
                    {
                        Required = true,
                        Multiple = false,
                        DisplayedContentTypes = [AddressConstants.City],
                    })
                )
                .WithField(AddressConstants.CodeField, field => field
                    .OfType("TextField")
                    .WithDisplayName("Code")
                    .WithDescription("An optional district code used for money-safe matching.")
                    .WithPosition("2")
                ));
    }

    private async Task DefineGeographicTypeAsync(
        string contentType,
        string partName,
        string displayName,
        string titleDisplayName,
        Action<ContentPartDefinitionBuilder> configurePart)
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(partName, configurePart);

        await _contentDefinitionManager.AlterTypeDefinitionAsync(contentType, type => type
            .WithDisplayName(displayName)
            .Creatable()
            .Listable()
            .Securable()
            .WithPart("TitlePart", part => part
                .WithPosition("0")
                .WithDisplayName(titleDisplayName)
                .WithEditor("UniqueTitle")
                .WithSettings(new TitlePartSettings
                {
                    RenderTitle = true,
                    Options = TitlePartOptions.EditableRequired,
                })
            )
            .WithPart(partName, part => part
                .WithPosition("1")
            )
        );
    }

    private Task DefineAddressPartAsync()
    {
        return _contentDefinitionManager.AlterPartDefinitionAsync(AddressConstants.AddressPart, part => part
            .Attachable()
            .Reusable()
            .WithDisplayName("Address")
            .WithDescription("Captures a postal address with content-item selectors for every geographic component except the postal code.")
            .WithField("AddressLine1", field => field
                .OfType("TextField")
                .WithDisplayName("Line 1")
                .WithPosition("1")
            )
            .WithField("AddressLine2", field => field
                .OfType("TextField")
                .WithDisplayName("Line 2")
                .WithPosition("2")
            )
            .WithField("Country", field => field
                .OfType("ContentPickerField")
                .WithDisplayName("Country")
                .WithPosition("3")
                .WithSettings(new ContentPickerFieldSettings
                {
                    Multiple = false,
                    DisplayedContentTypes = [AddressConstants.Country],
                })
            )
            .WithField("Region", field => field
                .OfType("ContentPickerField")
                .WithDisplayName("Region")
                .WithPosition("4")
                .WithSettings(new ContentPickerFieldSettings
                {
                    Multiple = false,
                    DisplayedContentTypes = [AddressConstants.Region],
                })
            )
            .WithField("County", field => field
                .OfType("ContentPickerField")
                .WithDisplayName("County")
                .WithPosition("5")
                .WithSettings(new ContentPickerFieldSettings
                {
                    Multiple = false,
                    DisplayedContentTypes = [AddressConstants.County],
                })
            )
            .WithField("City", field => field
                .OfType("ContentPickerField")
                .WithDisplayName("City")
                .WithPosition("6")
                .WithSettings(new ContentPickerFieldSettings
                {
                    Multiple = false,
                    DisplayedContentTypes = [AddressConstants.City],
                })
            )
            .WithField("District", field => field
                .OfType("ContentPickerField")
                .WithDisplayName("District")
                .WithPosition("7")
                .WithSettings(new ContentPickerFieldSettings
                {
                    Multiple = false,
                    DisplayedContentTypes = [AddressConstants.District],
                })
            )
            .WithField("PostalCode", field => field
                .OfType("TextField")
                .WithDisplayName("Postal code")
                .WithPosition("8")
            )
        );
    }
}
