using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using OrchardCore.ContentManagement.Metadata;
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
            .WithDisplayName("Omnichannel Contact")
            .WithDescription("Provides a way to configure a content type to act as an omnichannel contact record.")
        );

        await SchemaBuilder.CreateMapIndexTableAsync<OmnichannelContactIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("PrimaryCellPhoneNumber", column => column.WithLength(50))
            .Column<string>("NormalizedPrimaryCellPhoneNumber", column => column.WithLength(50))
            .Column<string>("PrimaryHomePhoneNumber", column => column.WithLength(50))
            .Column<string>("NormalizedPrimaryHomePhoneNumber", column => column.WithLength(50))
            .Column<string>("PrimaryEmailAddress", column => column.WithLength(255))
        );

        await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
            .CreateIndex("IDX_OmnichannelContactIndex_DocumentId",
                "DocumentId",
                "ContentItemId"
            )
        );

        await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
            .CreateIndex(
                "IDX_OmnichannelContactIndex_NormalizedPrimaryCellPhoneNumber",
                "DocumentId",
                "NormalizedPrimaryCellPhoneNumber")
        );

        await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
            .CreateIndex(
                "IDX_OmnichannelContactIndex_NormalizedPrimaryHomePhoneNumber",
                "DocumentId",
                "NormalizedPrimaryHomePhoneNumber")
        );

        return 2;
    }

    /// <summary>
    /// Updates the from1 async.
    /// </summary>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table =>
        {
            table.AddColumn<string>("NormalizedPrimaryCellPhoneNumber", column => column.WithLength(50));
            table.AddColumn<string>("NormalizedPrimaryHomePhoneNumber", column => column.WithLength(50));
        });

        await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
            .CreateIndex(
                "IDX_OmnichannelContactIndex_NormalizedPrimaryCellPhoneNumber",
                "DocumentId",
                "NormalizedPrimaryCellPhoneNumber")
        );

        await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
            .CreateIndex(
                "IDX_OmnichannelContactIndex_NormalizedPrimaryHomePhoneNumber",
                "DocumentId",
                "NormalizedPrimaryHomePhoneNumber")
        );

        return 2;
    }
}
