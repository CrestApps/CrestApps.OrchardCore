using CrestApps.OrchardCore.Fundraising.Indexes;
using CrestApps.OrchardCore.Fundraising.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Fundraising.Migrations;

/// <summary>
/// Creates the index of donations, and the attachable Fundraising campaign part.
/// </summary>
internal sealed class FundraisingMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public FundraisingMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<DonationIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("CampaignId", column => column.WithLength(26))
            .Column<string>("OrderId", column => column.WithLength(26))
            .Column<string>("DonorUserId", column => column.Nullable().WithLength(26))
            .Column<decimal>("CampaignAmount")
            .Column<decimal>("Refunded")
            .Column<DateTime>("CreatedUtc"),
            collection: FundraisingConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<DonationIndex>(table =>
        {
            table.CreateIndex("IDX_DonationIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_DonationIndex_Campaign", "CampaignId");
            table.CreateIndex("IDX_DonationIndex_Donor", "DonorUserId");
            table.CreateIndex("IDX_DonationIndex_Order", "OrderId");
        }, collection: FundraisingConstants.CollectionName);

        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(FundraisingCampaignPart), part => part
            .Attachable()
            .WithDisplayName("Fundraising campaign")
            .WithDescription("Makes a product a campaign donors give their own amount to, toward a goal, with a tax receipt.")
        );

        return 1;
    }
}
