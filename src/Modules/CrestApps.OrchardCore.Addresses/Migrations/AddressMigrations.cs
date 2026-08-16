using CrestApps.OrchardCore.Addresses;
using OrchardCore.ContentFields.Settings;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Addresses.Migrations;

/// <summary>
/// Creates the country, region, and city content types together with their information parts and the
/// reusable, attachable address part.
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
    /// Defines the country, region, and city content types, their parts, and the reusable address part.
    /// </summary>
    /// <returns>The next migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(AddressConstants.CountryPart, part => part
            .WithDisplayName("Country")
            .WithDescription("Provides the ISO information for a country.")
            .WithField("Code", field => field
                .OfType("TextField")
                .WithDisplayName("ISO code")
                .WithDescription("The ISO 3166-1 alpha-2 country code, for example US or CA.")
                .WithPosition("1")
                .WithSettings(new TextFieldSettings
                {
                    Required = true,
                })
            )
        );

        await _contentDefinitionManager.AlterTypeDefinitionAsync(AddressConstants.Country, type => type
            .WithDisplayName("Country")
            .Creatable()
            .Listable()
            .Securable()
            .WithPart("TitlePart", part => part
                .WithPosition("0")
                .WithDisplayName("Country name")
                .WithEditor("UniqueTitle")
                .WithSettings(new TitlePartSettings
                {
                    RenderTitle = true,
                    Options = TitlePartOptions.EditableRequired,
                })
            )
            .WithPart(AddressConstants.CountryPart, part => part
                .WithPosition("1")
            )
        );

        await _contentDefinitionManager.AlterPartDefinitionAsync(AddressConstants.RegionPart, part => part
            .WithDisplayName("Region")
            .WithDescription("Provides the parent country and abbreviation for a state, province, or region.")
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
            .WithField("Abbreviation", field => field
                .OfType("TextField")
                .WithDisplayName("Abbreviation")
                .WithDescription("The subdivision abbreviation, for example CA for California.")
                .WithPosition("2")
            )
        );

        await _contentDefinitionManager.AlterTypeDefinitionAsync(AddressConstants.Region, type => type
            .WithDisplayName("Region")
            .Creatable()
            .Listable()
            .Securable()
            .WithPart("TitlePart", part => part
                .WithPosition("0")
                .WithDisplayName("Region name")
                .WithEditor("UniqueTitle")
                .WithSettings(new TitlePartSettings
                {
                    RenderTitle = true,
                    Options = TitlePartOptions.EditableRequired,
                })
            )
            .WithPart(AddressConstants.RegionPart, part => part
                .WithPosition("1")
            )
        );

        await _contentDefinitionManager.AlterPartDefinitionAsync(AddressConstants.CityPart, part => part
            .WithDisplayName("City")
            .WithDescription("Provides the parent region for a city.")
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
        );

        await _contentDefinitionManager.AlterTypeDefinitionAsync(AddressConstants.City, type => type
            .WithDisplayName("City")
            .Creatable()
            .Listable()
            .Securable()
            .WithPart("TitlePart", part => part
                .WithPosition("0")
                .WithDisplayName("City name")
                .WithEditor("UniqueTitle")
                .WithSettings(new TitlePartSettings
                {
                    RenderTitle = true,
                    Options = TitlePartOptions.EditableRequired,
                })
            )
            .WithPart(AddressConstants.CityPart, part => part
                .WithPosition("1")
            )
        );

        await _contentDefinitionManager.AlterPartDefinitionAsync(AddressConstants.AddressPart, part => part
            .Attachable()
            .Reusable()
            .WithDisplayName("Address")
            .WithDescription("Captures a postal address with optional country and region selectors.")
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
            .WithField("City", field => field
                .OfType("TextField")
                .WithDisplayName("City")
                .WithPosition("3")
            )
            .WithField("PostalCode", field => field
                .OfType("TextField")
                .WithDisplayName("Postal code")
                .WithPosition("4")
            )
            .WithField("Region", field => field
                .OfType("ContentPickerField")
                .WithDisplayName("Region")
                .WithPosition("5")
                .WithSettings(new ContentPickerFieldSettings
                {
                    Multiple = false,
                    DisplayedContentTypes = [AddressConstants.Region],
                })
            )
            .WithField("Country", field => field
                .OfType("ContentPickerField")
                .WithDisplayName("Country")
                .WithPosition("6")
                .WithSettings(new ContentPickerFieldSettings
                {
                    Multiple = false,
                    DisplayedContentTypes = [AddressConstants.Country],
                })
            )
        );

        return 1;
    }
}
