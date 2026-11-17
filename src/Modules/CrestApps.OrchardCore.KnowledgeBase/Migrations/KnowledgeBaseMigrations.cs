using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.KnowledgeBase.Indexes;
using CrestApps.OrchardCore.Roles.Core.Models;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Autoroute.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Data.Migration;
using OrchardCore.Markdown.Models;
using OrchardCore.Title.Models;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.KnowledgeBase.Migrations;

/// <summary>
/// Adds the Knowledge Article part and type, and the article index. Who may read an article is the Content Access
/// Control feature's restricted <see cref="RolePickerPart"/> on the type.
/// </summary>
internal sealed class KnowledgeBaseMigrations : DataMigration
{
    /// <summary>
    /// The permission that read staff-only articles before Content Access Control decided who reads an article.
    /// </summary>
    internal const string StaffArticlesPermission = "ViewStaffKnowledgeArticles";

    private const string AudienceProperty = "Audience";

    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ISession _session;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    public KnowledgeBaseMigrations(
        IContentDefinitionManager contentDefinitionManager,
        ISession session,
        IServiceProvider serviceProvider,
        ILogger<KnowledgeBaseMigrations> logger)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _session = session;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<int> CreateAsync()
    {
        await DefinePartAsync();

        if (await _contentDefinitionManager.GetTypeDefinitionAsync(KnowledgeBaseConstants.ArticleType) is null)
        {
            await _contentDefinitionManager.AlterTypeDefinitionAsync(KnowledgeBaseConstants.ArticleType, type => type
                .WithDisplayName("Knowledge Article")
                .Draftable()
                .Versionable()
                .Listable()
                .Creatable()
                .Securable()
                .WithPart<TitlePart>(part => part
                    .WithPosition("1")
                    .WithSettings(new TitlePartSettings { Options = TitlePartOptions.EditableRequired }))
                .WithPart(KnowledgeBaseConstants.PartName, part => part.WithPosition("2"))
                .WithPart<MarkdownBodyPart>(part => part.WithPosition("3"))
                .WithPart<AutoroutePart>(part => part
                    .WithPosition("4")
                    .WithSettings(new AutoroutePartSettings
                    {
                        AllowCustomPath = true,
                        Pattern = "help/{{ ContentItem | display_text | slugify }}",
                    })));
        }

        await AddWhoCanReadItAsync();
        await CreateArticleIndexTableAsync(SchemaBuilder);

        return 2;
    }

    /// <summary>
    /// Moves who may read each article from its own audience to Content Access Control: an article for everyone picks
    /// no role, one for signed-in people picks Authenticated, and a staff one the roles that held
    /// <see cref="StaffArticlesPermission"/>.
    /// </summary>
    /// <returns>The new version.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await DefinePartAsync();
        await AddWhoCanReadItAsync();

        var staffRoles = await RolePermissionMigrations.GetRolesHoldingAsync(_serviceProvider, StaffArticlesPermission);
        var articles = await _session.Query<ContentItem, ContentItemIndex>(index => index.ContentType == KnowledgeBaseConstants.ArticleType).ListAsync();
        var count = 0;

        // Every version, so restoring an old one keeps who may read it. Saved before the migration ends, so an article
        // is never open to everyone in between.
        foreach (var article in articles)
        {
            MoveAudienceToRoles(article, staffRoles);
            await _session.SaveAsync(article, checkConcurrency: false);
            count++;
        }

        // The category index led with the audience. The nullable Audience column itself stays, unused, so a node still
        // on version 1 keeps working during a rolling upgrade; a later release may drop it.
        await SchemaBuilder.AlterIndexTableAsync<KnowledgeArticleIndex>(table =>
            table.DropIndex("IDX_KnowledgeArticleIndex_Category"));

        await SchemaBuilder.AlterIndexTableAsync<KnowledgeArticleIndex>(table =>
            table.CreateIndex("IDX_KnowledgeArticleIndex_Category", "Category", "DocumentId"));

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Moved who may read {Count} knowledge article versions to Content Access Control; staff articles went to the roles {Roles}.", count, string.Join(", ", staffRoles));
        }

        return 2;
    }

    /// <summary>
    /// Creates the table of <see cref="KnowledgeArticleIndex"/>.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task that completes when the table exists.</returns>
    internal static async Task CreateArticleIndexTableAsync(ISchemaBuilder schemaBuilder)
    {
        await schemaBuilder.CreateMapIndexTableAsync<KnowledgeArticleIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("Title", column => column.WithLength(KnowledgeArticleIndexProvider.MaxTitleLength))
            .Column<string>("Summary", column => column.WithLength(KnowledgeArticleIndexProvider.MaxSummaryLength))
            .Column<string>("Category", column => column.WithLength(100))
            .Column<DateTime>("PublishedUtc", column => column.Nullable())
            .Column<int>("HelpfulCount", column => column.Nullable()));

        await schemaBuilder.AlterIndexTableAsync<KnowledgeArticleIndex>(table =>
        {
            table.CreateIndex("IDX_KnowledgeArticleIndex_Category", "Category", "DocumentId");
            table.CreateIndex("IDX_KnowledgeArticleIndex_ContentItemId", "ContentItemId");
        });
    }

    /// <summary>
    /// Picks the roles an article written before may be read by, from the audience it had, and removes the audience.
    /// </summary>
    /// <param name="article">The article.</param>
    /// <param name="staffRoles">The roles that held <see cref="StaffArticlesPermission"/>.</param>
    internal static void MoveAudienceToRoles(ContentItem article, IReadOnlyCollection<string> staffRoles)
    {
        var part = ((JsonObject)article.Content)[KnowledgeBaseConstants.PartName] as JsonObject;
        var audience = part?[AudienceProperty];

        string[] roles = AudienceName(audience) switch
        {
            "1" or "SignedIn" => [OrchardCoreConstants.Roles.Authenticated],

            // Administrators read every article anyway; picking the role keeps a staff article restricted even when no
            // other role held the permission.
            "2" or "Staff" => [.. staffRoles.Where(role => !string.Equals(role, OrchardCoreConstants.Roles.Administrator, StringComparison.OrdinalIgnoreCase)), OrchardCoreConstants.Roles.Administrator],
            _ => [],
        };

        part?.Remove(AudienceProperty);
        article.Alter<RolePickerPart>(picker => picker.RoleNames = roles);
    }

    private static string AudienceName(JsonNode audience)
        => audience?.GetValueKind() switch
        {
            JsonValueKind.Number => audience.GetValue<int>().ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonValueKind.String => audience.GetValue<string>(),
            _ => null,
        };

    private Task DefinePartAsync()
        => _contentDefinitionManager.AlterPartDefinitionAsync(KnowledgeBaseConstants.PartName, part => part
            .Attachable()
            .WithDisplayName("Knowledge Article")
            .WithDescription("Makes a content item a help article: its category and summary, and what readers said about it."));

    // "Who can read it": Content Access Control's role picker, restricted, with no role meaning everyone.
    private Task AddWhoCanReadItAsync()
        => _contentDefinitionManager.AlterTypeDefinitionAsync(KnowledgeBaseConstants.ArticleType, type => type
            .WithPart(nameof(RolePickerPart), part => part
                .WithPosition("2.5")
                .WithDisplayName("Who can read it")
                .WithSettings(new RolePickerPartSettings
                {
                    AllowSelectMultiple = true,
                    Hint = "Leave empty for everyone. Pick Authenticated for anyone who is signed in, or the roles of the staff who may read it.",
                })
                .WithSettings(new RolePickerPartContentAccessControlSettings
                {
                    IsContentRestricted = true,
                })));
}
