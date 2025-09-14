using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentFields.Settings;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Flows.Models;
using OrchardCore.Title.Models;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

public sealed class OmnichannelContactsMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public OmnichannelContactsMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(OmnichannelConstants.ContentParts.OmnichannelContactInfo, part => part
            .Attachable()
            .WithDisplayName("Omnichannel Contact Info Part")
            .WithDescription("Provides a way to capture contact info")
            .WithField<TextField>("FirstName", field => field
                .WithDisplayName("First name")
                .WithSettings(new TextFieldSettings()
                {
                    Required = true,
                })
            )
            .WithField<TextField>("MiddleName", field => field
                .WithDisplayName("Middle name")
            )
            .WithField<TextField>("LastName", field => field
                .WithDisplayName("Last name")
                .WithSettings(new TextFieldSettings()
                {
                    Required = true,
                })
            )
        );

        await _contentDefinitionManager.AlterTypeDefinitionAsync(OmnichannelConstants.ContentTypes.OmnichannelContact, type => type
            .Securable()
            .Creatable()
            .Listable()
            .DisplayedAs("Contact")
            .Stereotype(OmnichannelConstants.Sterotypes.OmnichannelContact)
            .WithPart<TitlePart>(part => part
                .WithSettings(new TitlePartSettings()
                {
                    Options = TitlePartOptions.GeneratedHidden,
                    Pattern = $"{{{{ ContentItem.Content.{OmnichannelConstants.ContentParts.OmnichannelContactInfo}.FirstName.Text | append: ' ' | append: ContentItem.Content.{OmnichannelConstants.ContentParts.OmnichannelContactInfo}.LastName.Text }}}}",
                })
                .WithPosition("1")
            )
            .WithPart(OmnichannelConstants.ContentParts.OmnichannelContactInfo, part =>
                part.WithPosition("5")
            )
            .WithPart<BagPart>(OmnichannelConstants.NamedParts.ContactMethods, part => part
                .WithDisplayName("Contact Info")
                .WithSettings(new BagPartSettings()
                {
                    ContainedStereotypes = [OmnichannelConstants.Sterotypes.ContactMethod],
                })
                .WithPosition("10")
            )
        );

        await SchemaBuilder.CreateMapIndexTableAsync<OmnichannelContactIndex>(table => table
           .Column<string>("ContentItemId", column => column.WithLength(26))
           .Column<string>("FirstName", column => column.WithLength(50))
           .Column<string>("LastName", column => column.WithLength(50))
           .Column<string>("PrimaryCellPhoneNumber", column => column.WithLength(50))
           .Column<string>("PrimaryHomePhoneNumber", column => column.WithLength(50))
           .Column<string>("PrimaryEmailAddress", column => column.WithLength(255))
        );

        await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
            .CreateIndex("IDX_OmnichannelContactIndex_DocumentId",
                "DocumentId",
                "ContentItemId",
                "FirstName",
                "LastName"
            )
        );

        return 1;
    }
}
