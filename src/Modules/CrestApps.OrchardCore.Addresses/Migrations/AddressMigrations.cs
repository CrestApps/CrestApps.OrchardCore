using CrestApps.OrchardCore.Addresses;
using OrchardCore.ContentFields.Settings;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Addresses.Migrations;

/// <summary>
/// Defines the Country content type and its part. Regions come with each country's address format instead of being
/// content, so they are not defined here.
/// </summary>
public sealed class AddressMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public AddressMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    // Sites that ran the earlier steps are already at 3, so nothing runs again for them.
    public async Task<int> CreateAsync()
    {
        await DefineCountryAsync();

        return 3;
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
}
