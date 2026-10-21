using CrestApps.OrchardCore.Addresses;
using OrchardCore.ContentFields.Settings;
using OrchardCore.ContentManagement.Metadata;
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

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(AddressConstants.CountryPart, part => part
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

        return 1;
    }
}
