using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

/// <summary>
/// Defines database migrations for the Migrations module.
/// </summary>
public sealed class OmnichannelContactsMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContactsMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    public OmnichannelContactsMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Creates a new async.
    /// </summary>
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

    /// <summary>
    /// Updates the from1 async.
    /// </summary>
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

    /// <summary>
    /// Updates existing omnichannel contact types so they always store contact methods in the fixed ContactMethods bag.
    /// </summary>
    public async Task<int> UpdateFrom2Async()
    {
        var contentTypeDefinitions = await _contentDefinitionManager.ListTypeDefinitionsAsync();

        foreach (var contentTypeDefinition in contentTypeDefinitions)
        {
            if (!OmnichannelContactDefinitionService.HasOmnichannelContactPart(contentTypeDefinition) ||
                !OmnichannelContactDefinitionService.NeedsContactMethodsBagUpdate(contentTypeDefinition))
            {
                continue;
            }

            await _contentDefinitionManager.AlterTypeDefinitionAsync(contentTypeDefinition.Name, OmnichannelContactDefinitionService.ConfigureContactMethodsBagPart);
        }

        return 3;
    }
}
