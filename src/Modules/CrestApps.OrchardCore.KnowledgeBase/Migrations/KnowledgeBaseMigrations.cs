using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.KnowledgeBase.Indexes;
using CrestApps.OrchardCore.KnowledgeBase.Models;
using CrestApps.OrchardCore.Roles.Core.Models;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Autoroute.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Markdown.Models;
using OrchardCore.Taxonomies.Settings;
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

    // Orchard Core's Wysiwyg editor of a Markdown body, instead of its plain one.
    private const string WysiwygEditor = "Wysiwyg";

    // The label of the article's body on its editor, instead of the part's name.
    private const string BodyDisplayName = "Body";

    // Where an article kept who may read it before Content Access Control did.
    private const string AudienceProperty = "Audience";

    // The label version 5 gave the role picker on the article editor, before it kept Content Access Control's own.
    private const string OldWhoCanReadItLabel = "Who can read it";

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
                    .WithPosition(KnowledgeArticlePartPositions.Of(nameof(TitlePart)))
                    .WithSettings(new TitlePartSettings { Options = TitlePartOptions.EditableRequired }))
                .WithPart(KnowledgeBaseConstants.PartName, part => part.WithPosition(KnowledgeArticlePartPositions.Of(KnowledgeBaseConstants.PartName)))
                .WithPart<MarkdownBodyPart>(part => part
                    .WithDisplayName(BodyDisplayName)
                    .WithPosition(KnowledgeArticlePartPositions.Of(nameof(MarkdownBodyPart)))
                    .WithEditor(WysiwygEditor))
                .WithPart<AutoroutePart>(part => part
                    .WithPosition(KnowledgeArticlePartPositions.Of(nameof(AutoroutePart)))
                    .WithSettings(new AutoroutePartSettings
                    {
                        AllowCustomPath = true,
                        Pattern = "help/{{ ContentItem | display_text | slugify }}",
                    })));
        }

        await SchemaBuilder.CreateMapIndexTableAsync<KnowledgeArticleIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("Title", column => column.WithLength(KnowledgeArticleIndexProvider.MaxTitleLength))
            .Column<string>("Summary", column => column.WithLength(KnowledgeArticleIndexProvider.MaxSummaryLength))
            .Column<string>("CategoryId", column => column.Nullable().WithLength(26))
            .Column<DateTime>("PublishedUtc", column => column.Nullable())
            .Column<int>("HelpfulCount", column => column.Nullable()));

        await SchemaBuilder.AlterIndexTableAsync<KnowledgeArticleIndex>(table =>
        {
            table.CreateIndex("IDX_KnowledgeArticleIndex_CategoryId", "CategoryId", "DocumentId");
            table.CreateIndex("IDX_KnowledgeArticleIndex_ContentItemId", "ContentItemId");
        });

        ConvertCategoriesOnceCommitted(await DefineCategoriesAsync());
        await AddWhoCanReadItAsync();
        await CreateFeedbackIndexAsync(SchemaBuilder);
        await DefineWidgetsAsync();

        return 8;
    }

    /// <summary>
    /// Makes an article's category a term of the Knowledge Base Categories taxonomy instead of text: the term type, the
    /// taxonomy and the Category field of the Knowledge Article type, and the index's category column. Once the
    /// migration is committed, each distinct text category becomes a term and the articles get it.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<KnowledgeArticleIndex>(table =>
        {
            table.AddColumn<string>("CategoryId", column => column.Nullable().WithLength(26));
            table.CreateIndex("IDX_KnowledgeArticleIndex_CategoryId", "Audience", "CategoryId", "DocumentId");
        });

        ConvertCategoriesOnceCommitted(await DefineCategoriesAsync());

        return 2;
    }

    /// <summary>
    /// Gives the Knowledge Article type's parts whole positions, which Orchard Core's content type editor needs (version 2
    /// put the category's part at 2.5, and that page failed), and makes the body's editor the Wysiwyg one where it is
    /// still the default; an editor an administrator chose stays.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        // The stored definition, not the cached copy, which doesn't show what version 2 changed when both ran together.
        var type = await _contentDefinitionManager.LoadTypeDefinitionAsync(KnowledgeBaseConstants.ArticleType);

        if (type is null)
        {
            return 3;
        }

        var positions = KnowledgeArticlePartPositions.Renumber([.. type.Parts.Select(part => (part.Name, part.GetSettings<ContentTypePartSettings>().Position))]);
        var body = type.Parts.FirstOrDefault(part => part.PartDefinition.Name == nameof(MarkdownBodyPart));
        var bodyEditorIsDefault = body is not null && string.IsNullOrEmpty(body.Editor());

        if (positions.Count > 0 || bodyEditorIsDefault)
        {
            await _contentDefinitionManager.AlterTypeDefinitionAsync(KnowledgeBaseConstants.ArticleType, builder =>
            {
                foreach (var (name, position) in positions)
                {
                    builder.WithPart(name, part => part.WithPosition(position));
                }

                if (bodyEditorIsDefault)
                {
                    builder.WithPart(body.Name, part => part.WithEditor(WysiwygEditor));
                }
            });
        }

        return 3;
    }

    /// <summary>
    /// Puts the category's part before the body where version 2 left them in the same place (a site that went from
    /// version 1 to 3 in one start, whose step 3 read the type as it was before step 2), and names the body's part Body
    /// on the article editor, unless an administrator named it.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> UpdateFrom3Async()
    {
        var type = await _contentDefinitionManager.LoadTypeDefinitionAsync(KnowledgeBaseConstants.ArticleType);

        if (type is null)
        {
            return 4;
        }

        var positions = KnowledgeArticlePartPositions.Renumber([.. type.Parts.Select(part => (part.Name, part.GetSettings<ContentTypePartSettings>().Position))]);
        var body = type.Parts.FirstOrDefault(part => part.PartDefinition.Name == nameof(MarkdownBodyPart));
        var nameBody = body is not null && string.IsNullOrEmpty(body.GetSettings<ContentTypePartSettings>().DisplayName);

        if (positions.Count > 0 || nameBody)
        {
            await _contentDefinitionManager.AlterTypeDefinitionAsync(KnowledgeBaseConstants.ArticleType, builder =>
            {
                foreach (var (name, position) in positions)
                {
                    builder.WithPart(name, part => part.WithPosition(position));
                }

                if (nameBody)
                {
                    builder.WithPart(body.Name, part => part.WithDisplayName(BodyDisplayName));
                }
            });
        }

        return 4;
    }

    /// <summary>
    /// Moves who may read each article from its own audience to Content Access Control: the type gets the restricted
    /// role picker, and an article for everyone picks no role, one for signed-in people picks
    /// Authenticated, and a staff one the roles that held <see cref="StaffArticlesPermission"/> and Administrator.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> UpdateFrom4Async()
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

        // The category index led with the audience. The Audience column itself stays, unused, so a node still on version
        // 4 keeps working during a rolling upgrade; a later release may drop it.
        await SchemaBuilder.AlterIndexTableAsync<KnowledgeArticleIndex>(table =>
            table.DropIndex("IDX_KnowledgeArticleIndex_CategoryId"));

        await SchemaBuilder.AlterIndexTableAsync<KnowledgeArticleIndex>(table =>
            table.CreateIndex("IDX_KnowledgeArticleIndex_CategoryId", "CategoryId", "DocumentId"));

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Moved who may read {Count} knowledge article versions to Content Access Control; staff articles went to the roles {Roles}.", count, string.Join(", ", staffRoles));
        }

        return 5;
    }

    /// <summary>
    /// Shows the role picker on the article editor under Content Access Control's own name, Role Picker, where version 5
    /// named it "Who can read it"; a name an administrator gave it stays.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> UpdateFrom5Async()
    {
        var type = await _contentDefinitionManager.LoadTypeDefinitionAsync(KnowledgeBaseConstants.ArticleType);
        var picker = type?.Parts.FirstOrDefault(part => part.Name == KnowledgeBaseConstants.WhoCanReadItPart);
        var settings = picker?.GetSettings<ContentTypePartSettings>();

        if (settings is not null && string.Equals(settings.DisplayName, OldWhoCanReadItLabel, StringComparison.Ordinal))
        {
            settings.DisplayName = null;

            await _contentDefinitionManager.AlterTypeDefinitionAsync(KnowledgeBaseConstants.ArticleType, builder =>
                builder.WithPart(KnowledgeBaseConstants.WhoCanReadItPart, part => part.WithSettings(settings)));
        }

        return 6;
    }

    /// <summary>
    /// Keeps each reader's answer to "Was this helpful?" as a dated record in the Knowledge Base's collection, for the
    /// feedback report; the counters on the article stay, for sorting.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> UpdateFrom6Async()
    {
        await CreateFeedbackIndexAsync(SchemaBuilder);

        return 7;
    }

    /// <summary>
    /// Adds the help center's widgets: its search box, its categories and an article's related articles.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> UpdateFrom7Async()
    {
        await DefineWidgetsAsync();

        return 8;
    }

    /// <summary>
    /// Creates the table of <see cref="KnowledgeArticleFeedbackIndex"/> in the Knowledge Base's collection.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task that completes when the table exists.</returns>
    internal static async Task CreateFeedbackIndexAsync(ISchemaBuilder schemaBuilder)
    {
        await schemaBuilder.CreateMapIndexTableAsync<KnowledgeArticleFeedbackIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("CategoryId", column => column.Nullable().WithLength(26))
            .Column<bool>("Helpful")
            .Column<bool>("HasComment")
            .Column<DateTime>("CreatedUtc")
            .Column<string>("UserId", column => column.Nullable().WithLength(26)),
            collection: KnowledgeBaseConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<KnowledgeArticleFeedbackIndex>(table =>
        {
            table.CreateIndex("IDX_KBFeedback_Created", "CreatedUtc", "DocumentId");
            table.CreateIndex("IDX_KBFeedback_Article", "ContentItemId", "CreatedUtc");
            table.CreateIndex("IDX_KBFeedback_ItemId", "ItemId");
            table.CreateIndex("IDX_KBFeedback_User", "UserId");
        }, collection: KnowledgeBaseConstants.CollectionName);
    }

    // The widgets the help center and its articles share; the Knowledge Base recipe places them.
    private async Task DefineWidgetsAsync()
    {
        await DefineWidgetAsync(nameof(HelpCenterSearchPart), "Help center search", "A search box that searches the help center's articles.");
        await DefineWidgetAsync(nameof(KnowledgeBaseCategoriesPart), "Knowledge base categories", "The help center's categories, with how many articles a reader may open in each.");
        await DefineWidgetAsync(nameof(RelatedArticlesPart), "Related articles", "On an article, other articles about the same thing: those of its category first, then those whose title or summary share its words.");
    }

    private async Task DefineWidgetAsync(string partName, string displayName, string description)
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(partName, part => part
            .WithDisplayName(displayName)
            .WithDescription(description));

        var typeName = KnowledgeBaseConstants.WidgetTypeOf(partName);

        if (await _contentDefinitionManager.GetTypeDefinitionAsync(typeName) is null)
        {
            await _contentDefinitionManager.AlterTypeDefinitionAsync(typeName, type => type
                .WithDisplayName(displayName)
                .WithDescription(description)
                .Stereotype("Widget")
                .WithPart(partName));
        }
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

    /// <summary>
    /// Gives the Knowledge Article type Content Access Control's restricted role picker, under its own name (Role Picker),
    /// with no role meaning everyone. It goes after the title, with every part's final position in the same change, so positions stay
    /// whole and distinct; a type that already has it keeps its place.
    /// </summary>
    /// <returns>A task that completes when the type has the part.</returns>
    internal async Task AddWhoCanReadItAsync()
    {
        var article = await _contentDefinitionManager.LoadTypeDefinitionAsync(KnowledgeBaseConstants.ArticleType);

        if (article is null)
        {
            return;
        }

        var positions = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!article.Parts.Any(part => part.Name == KnowledgeBaseConstants.WhoCanReadItPart))
        {
            List<(string Name, string Position)> parts = [.. article.Parts.Select(part => (part.Name, part.GetSettings<ContentTypePartSettings>().Position))];
            var place = KnowledgeArticlePartPositions.ForAdded(parts, KnowledgeBaseConstants.WhoCanReadItPart);
            parts.Add((KnowledgeBaseConstants.WhoCanReadItPart, place));

            foreach (var (name, position) in KnowledgeArticlePartPositions.Renumber(parts))
            {
                positions[name] = position;
            }

            positions.TryAdd(KnowledgeBaseConstants.WhoCanReadItPart, place);
        }

        await _contentDefinitionManager.AlterTypeDefinitionAsync(KnowledgeBaseConstants.ArticleType, type =>
        {
            foreach (var (name, position) in positions.Where(pair => pair.Key != KnowledgeBaseConstants.WhoCanReadItPart))
            {
                type.WithPart(name, part => part.WithPosition(position));
            }

            type.WithPart(KnowledgeBaseConstants.WhoCanReadItPart, part =>
            {
                if (positions.TryGetValue(KnowledgeBaseConstants.WhoCanReadItPart, out var position))
                {
                    part.WithPosition(position);
                }

                part.WithSettings(new RolePickerPartSettings
                    {
                        AllowSelectMultiple = true,
                        Hint = "Leave empty for everyone. Pick Authenticated for anyone who is signed in, or the roles of the staff who may read it.",
                    })
                    .WithSettings(new RolePickerPartContentAccessControlSettings
                    {
                        IsContentRestricted = true,
                    });
            });
        });
    }

    // The term type, and the Category taxonomy field of the Knowledge Article type with the taxonomy it uses (kept when an
    // administrator already chose one). The taxonomy itself, and the terms of the old text categories, are made after
    // the migration is committed, through the content manager.
    internal async Task<string> DefineCategoriesAsync()
    {
        if (await _contentDefinitionManager.GetTypeDefinitionAsync(KnowledgeBaseConstants.CategoryTermType) is null)
        {
            await _contentDefinitionManager.AlterTypeDefinitionAsync(KnowledgeBaseConstants.CategoryTermType, type => type
                .WithDisplayName("Knowledge category")
                .Listable(false)
                .Creatable(false)
                .WithPart<TitlePart>(part => part
                    .WithPosition("1")
                    .WithSettings(new TitlePartSettings { Options = TitlePartOptions.EditableRequired })));
        }

        var article = await _contentDefinitionManager.LoadTypeDefinitionAsync(KnowledgeBaseConstants.ArticleType);
        var taxonomyId = article?.Parts
            .FirstOrDefault(part => part.Name == KnowledgeBaseConstants.ArticleType)?
            .PartDefinition.Fields
            .FirstOrDefault(field => field.Name == KnowledgeBaseConstants.CategoryField)?
            .GetSettings<TaxonomyFieldSettings>()?.TaxonomyContentItemId;

        if (string.IsNullOrEmpty(taxonomyId))
        {
            taxonomyId = UniqueId.GenerateId();

            await _contentDefinitionManager.AlterPartDefinitionAsync(KnowledgeBaseConstants.ArticleType, part => part
                .WithField(KnowledgeBaseConstants.CategoryField, field => field
                    .OfType("TaxonomyField")
                    .WithDisplayName("Category")
                    .WithPosition("1")
                    .WithSettings(new TaxonomyFieldSettings
                    {
                        TaxonomyContentItemId = taxonomyId,
                        Unique = true,
                        Hint = "The help center lists the articles of a category together, and on the category's own page.",
                    })));

            // The category's part and every part's final position in one change, so it never shares a place with the body
            // (version 1 put the body where the category's part goes).
            List<(string Name, string Position)> parts = [.. (article?.Parts ?? [])
                .Where(part => part.Name != KnowledgeBaseConstants.ArticleType)
                .Select(part => (part.Name, part.GetSettings<ContentTypePartSettings>().Position))];
            var place = KnowledgeArticlePartPositions.ForAdded(parts, KnowledgeBaseConstants.ArticleType);
            parts.Add((KnowledgeBaseConstants.ArticleType, place));

            var positions = KnowledgeArticlePartPositions.Renumber(parts);

            if (positions.Count == 0)
            {
                positions = new Dictionary<string, string> { [KnowledgeBaseConstants.ArticleType] = place };
            }

            await _contentDefinitionManager.AlterTypeDefinitionAsync(KnowledgeBaseConstants.ArticleType, type =>
            {
                foreach (var (name, position) in positions)
                {
                    type.WithPart(name, part => part.WithPosition(position));
                }
            });
        }

        return taxonomyId;
    }

    private static string AudienceName(JsonNode audience)
        => audience?.GetValueKind() switch
        {
            JsonValueKind.Number => audience.GetValue<int>().ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonValueKind.String => audience.GetValue<string>(),
            _ => null,
        };

    private static void ConvertCategoriesOnceCommitted(string taxonomyId)
        => ShellScope.AddDeferredTask(scope => KnowledgeCategoryTerms.CreateAndConvertAsync(scope.ServiceProvider, taxonomyId));

    private Task DefinePartAsync()
        => _contentDefinitionManager.AlterPartDefinitionAsync(KnowledgeBaseConstants.PartName, part => part
            .Attachable()
            .WithDisplayName("Knowledge Article")
            .WithDescription("Makes a content item a help article: its summary, and what readers said about it."));
}
