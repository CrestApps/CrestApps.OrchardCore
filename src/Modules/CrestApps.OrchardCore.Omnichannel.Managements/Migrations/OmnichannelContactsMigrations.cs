using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
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
        await _contentDefinitionManager.AlterPartDefinitionAsync(OmnichannelConstants.ContentParts.OmnichannelContact, part => part
            .Attachable()
            .WithDescription("Omnichannel Contact")
            .WithSettings(new ContentSettings()
            {
                IsSystemDefined = true,
            })
        );

        await SchemaBuilder.CreateMapIndexTableAsync<OmnichannelContactIndex>(table => table
           .Column<string>("ContentItemId", column => column.WithLength(26))
           .Column<string>("PrimaryCellPhoneNumber", column => column.WithLength(50))
           .Column<string>("PrimaryHomePhoneNumber", column => column.WithLength(50))
           .Column<string>("PrimaryEmailAddress", column => column.WithLength(255))
        );

        await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
            .CreateIndex("IDX_OmnichannelContactIndex_DocumentId",
                "DocumentId",
                "ContentItemId"
            )
        );

        return 2;
    }

    public async Task<int> UpdateFrom1Async()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(OmnichannelConstants.ContentParts.OmnichannelContact, part => part
            .Attachable()
            .WithDisplayName("Omnichannel Contact")
            .WithDescription("Treats the content item as Contact.")
            .WithSettings(new ContentSettings()
            {
                IsSystemDefined = true,
            })
        );

        return 2;
    }
}
