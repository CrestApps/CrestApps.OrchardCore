using CrestApps.Core;
using CrestApps.OrchardCore.KnowledgeBase.Indexes;
using CrestApps.OrchardCore.KnowledgeBase.Models;
using OrchardCore.Autoroute.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Markdown.Models;
using OrchardCore.Taxonomies.Settings;
using OrchardCore.Title.Models;
using YesSql.Sql;

namespace CrestApps.OrchardCore.KnowledgeBase.Migrations;

/// <summary>
/// Adds the Knowledge Article part and type, and the article index.
/// </summary>
internal sealed class KnowledgeBaseMigrations : DataMigration
{
    // Orchard Core's Wysiwyg editor of a Markdown body, instead of its plain one.
    private const string WysiwygEditor = "Wysiwyg";

    // The label of the article's body on its editor, instead of the part's name.
    private const string BodyDisplayName = "Body";

    private readonly IContentDefinitionManager _contentDefinitionManager;

    public KnowledgeBaseMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(KnowledgeBaseConstants.PartName, part => part
            .Attachable()
            .WithDisplayName("Knowledge Article")
            .WithDescription("Makes a content item a help article: who may read it, its summary, and what readers said about it."));

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
            .Column<int>("Audience")
            .Column<DateTime>("PublishedUtc", column => column.Nullable())
            .Column<int>("HelpfulCount", column => column.Nullable()));

        await SchemaBuilder.AlterIndexTableAsync<KnowledgeArticleIndex>(table =>
        {
            table.CreateIndex("IDX_KnowledgeArticleIndex_CategoryId", "Audience", "CategoryId", "DocumentId");
            table.CreateIndex("IDX_KnowledgeArticleIndex_ContentItemId", "ContentItemId");
        });

        ConvertCategoriesOnceCommitted(await DefineCategoriesAsync());

        return 4;
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

    private static void ConvertCategoriesOnceCommitted(string taxonomyId)
        => ShellScope.AddDeferredTask(scope => KnowledgeCategoryTerms.CreateAndConvertAsync(scope.ServiceProvider, taxonomyId));

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
            parts.Add((KnowledgeBaseConstants.ArticleType, KnowledgeArticlePartPositions.Of(KnowledgeBaseConstants.ArticleType)));

            var positions = KnowledgeArticlePartPositions.Renumber(parts);

            if (positions.Count == 0)
            {
                positions = new Dictionary<string, string> { [KnowledgeBaseConstants.ArticleType] = KnowledgeArticlePartPositions.Of(KnowledgeBaseConstants.ArticleType) };
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
}
