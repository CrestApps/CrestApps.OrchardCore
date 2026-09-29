using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Lists.Models;
using OrchardCore.Title.Models;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

/// <summary>
/// Creates the CRM parts, the <c>Account</c> content type, the lead and opportunity indexes, and seeds the lead
/// statuses and opportunity stages.
/// </summary>
/// <remarks>
/// The migration never alters a content type that already exists, so an administrator's changes to the
/// <c>Account</c> type survive every upgrade, and it creates no contact, lead or opportunity type: those are the
/// tenant's to name.
/// </remarks>
public sealed class CrmMigrations : OmnichannelIndexMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrmMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="store">The YesSql store.</param>
    /// <param name="dbConnectionAccessor">The database connection accessor.</param>
    /// <param name="logger">The logger.</param>
    public CrmMigrations(
        IContentDefinitionManager contentDefinitionManager,
        IStore store,
        IDbConnectionAccessor dbConnectionAccessor,
        ILogger<CrmMigrations> logger)
        : base(store, dbConnectionAccessor, logger)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Creates the CRM parts, the account type, the indexes and the seeded catalogs.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(OmnichannelConstants.ContentParts.Lead, part => part
            .Attachable()
            .WithDisplayName("Lead")
            .WithDescription("Marks a contact type as a lead type: its records are called and texted like contacts but kept apart from them until they are converted. Attach it together with the Omnichannel Contact part."));

        await _contentDefinitionManager.AlterPartDefinitionAsync(OmnichannelConstants.ContentParts.Account, part => part
            .Attachable()
            .WithDisplayName("Account")
            .WithDescription("Marks a content type as an account, the company or household that contacts and opportunities belong to."));

        await _contentDefinitionManager.AlterPartDefinitionAsync(OmnichannelConstants.ContentParts.Opportunity, part => part
            .Attachable()
            .WithDisplayName("Opportunity")
            .WithDescription("Marks a content type as an opportunity, a deal in progress with a stage, an amount and a close date. Attach it to one content type per kind of deal."));

        if (await _contentDefinitionManager.GetTypeDefinitionAsync(OmnichannelConstants.ContentTypes.Account) is null)
        {
            var definitions = await _contentDefinitionManager.ListTypeDefinitionsAsync();
            var containedContentTypes = definitions
                .Where(Core.Services.OmnichannelRecordKinds.IsAccountChild)
                .Select(definition => definition.Name)
                .ToArray();

            await _contentDefinitionManager.AlterTypeDefinitionAsync(OmnichannelConstants.ContentTypes.Account, type => type
                .WithDisplayName("Account")
                .Creatable()
                .Listable()
                .Securable()
                .WithPart<TitlePart>(part => part
                    .WithPosition("1"))
                .WithPart(OmnichannelConstants.ContentParts.Account, part => part
                    .WithPosition("2")
                    .WithSettings(new Core.Models.AccountPartSettings
                    {
                        OfferedContentTypes = containedContentTypes,
                    }))
                .WithPart(OmnichannelConstants.ContentParts.List, part => part
                    .WithPosition("3")
                    .WithSettings(new ListPartSettings
                    {
                        PageSize = 20,
                        ShowHeader = true,
                        ContainedContentTypes = containedContentTypes,
                    })));
        }

        await CreateLeadIndexAsync(SchemaBuilder);
        await CreateOpportunityIndexAsync(SchemaBuilder);

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider
            .GetRequiredService<CrmCatalogSeeder>()
            .SeedAsync());

        return 1;
    }

    private static async Task CreateLeadIndexAsync(ISchemaBuilder schemaBuilder)
    {
        await schemaBuilder.CreateMapIndexTableAsync<LeadIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentType", column => column.WithLength(255))
            .Column<bool>("Published", column => column.NotNull().WithDefault(false))
            .Column<bool>("Latest", column => column.NotNull().WithDefault(false))
            .Column<string>("StatusId", column => column.WithLength(50))
            .Column<bool>("IsClosed", column => column.NotNull().WithDefault(false))
            .Column<bool>("IsConverted", column => column.NotNull().WithDefault(false))
            .Column<string>("Source", column => column.WithLength(255))
            .Column<string>("ListName", column => column.WithLength(255))
            .Column<string>("Rating", column => column.WithLength(20))
            .Column<string>("OwnerId", column => column.WithLength(50))
            .Column<string>("ConvertedContactItemId", column => column.WithLength(26))
            .Column<DateTime>("ConvertedUtc", column => column.Nullable())
            .Column<DateTime>("CreatedUtc", column => column.Nullable()));

        await schemaBuilder.AlterIndexTableAsync<LeadIndex>(table => table
            .CreateIndex("IDX_LeadIndex_DocumentId", "DocumentId", "ContentItemId", "Published", "Latest"));

        await schemaBuilder.AlterIndexTableAsync<LeadIndex>(table => table
            .CreateIndex("IDX_LeadIndex_Status", "StatusId", "IsConverted", "Published", "Latest"));

        await schemaBuilder.AlterIndexTableAsync<LeadIndex>(table => table
            .CreateIndex("IDX_LeadIndex_ListName", "ListName", "IsConverted", "Published", "Latest"));

        await schemaBuilder.AlterIndexTableAsync<LeadIndex>(table => table
            .CreateIndex("IDX_LeadIndex_Owner", "OwnerId", "IsConverted", "Published", "Latest"));
    }

    private static async Task CreateOpportunityIndexAsync(ISchemaBuilder schemaBuilder)
    {
        await schemaBuilder.CreateMapIndexTableAsync<OpportunityIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentType", column => column.WithLength(255))
            .Column<bool>("Published", column => column.NotNull().WithDefault(false))
            .Column<bool>("Latest", column => column.NotNull().WithDefault(false))
            .Column<string>("StageId", column => column.WithLength(50))
            .Column<bool>("IsClosed", column => column.NotNull().WithDefault(false))
            .Column<bool>("IsWon", column => column.NotNull().WithDefault(false))
            .Column<int>("Probability", column => column.Nullable())
            .Column<decimal>("Amount", column => column.Nullable())
            .Column<DateTime>("CloseDate", column => column.Nullable())
            .Column<string>("OwnerId", column => column.WithLength(50))
            .Column<string>("AccountContentItemId", column => column.WithLength(26))
            .Column<string>("CampaignId", column => column.WithLength(50))
            .Column<string>("PrimaryContactItemId", column => column.WithLength(26))
            .Column<string>("Source", column => column.WithLength(255))
            .Column<string>("ConvertedFromLeadItemId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc", column => column.Nullable()));

        await schemaBuilder.AlterIndexTableAsync<OpportunityIndex>(table => table
            .CreateIndex("IDX_OpportunityIndex_DocumentId", "DocumentId", "ContentItemId", "Published", "Latest"));

        await schemaBuilder.AlterIndexTableAsync<OpportunityIndex>(table => table
            .CreateIndex("IDX_OpportunityIndex_Stage", "StageId", "IsClosed", "Published", "Latest"));

        await schemaBuilder.AlterIndexTableAsync<OpportunityIndex>(table => table
            .CreateIndex("IDX_OpportunityIndex_Account", "AccountContentItemId", "Published", "Latest"));

        await schemaBuilder.AlterIndexTableAsync<OpportunityIndex>(table => table
            .CreateIndex("IDX_OpportunityIndex_CloseDate", "CloseDate", "IsClosed", "Published", "Latest"));
    }
}
