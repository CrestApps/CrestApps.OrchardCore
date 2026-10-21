using CrestApps.OrchardCore.Memberships.Indexes;
using CrestApps.OrchardCore.Memberships.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Memberships.Migrations;

/// <summary>
/// Creates the index of memberships, and the attachable Membership part.
/// </summary>
internal sealed class MembershipMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public MembershipMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<MembershipIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("UserName", column => column.Nullable().WithLength(255))
            .Column<int>("Status")
            .Column<DateTime>("EndsUtc", column => column.Nullable())
            .Column<DateTime>("CreatedUtc"),
            collection: MembershipsConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<MembershipIndex>(table =>
        {
            table.CreateIndex("IDX_MembershipIndex_User", "UserId");
            table.CreateIndex("IDX_MembershipIndex_Ends", "Status", "EndsUtc");
        }, collection: MembershipsConstants.CollectionName);

        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(MembershipPart), part => part
            .Attachable()
            .WithDisplayName("Membership")
            .WithDescription("Makes a product a membership: buying it puts the buyer in roles for its length.")
        );

        return 1;
    }
}
