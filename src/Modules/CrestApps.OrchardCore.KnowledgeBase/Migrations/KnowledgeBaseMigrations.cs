using CrestApps.Core;
using CrestApps.OrchardCore.KnowledgeBase.Indexes;
using CrestApps.OrchardCore.KnowledgeBase.Models;
using OrchardCore.Autoroute.Models;
using OrchardCore.ContentManagement.Metadata;
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

        await DefineCategoriesAsync();

        return 2;
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

        await DefineCategoriesAsync();

        return 2;
    }

    // The term type, and the Category taxonomy field of the Knowledge Article type with the taxonomy it uses (kept when an
    // administrator already chose one). The taxonomy itself, and the terms of the old text categories, are made after
    // the migration is committed, through the content manager.
    private async Task DefineCategoriesAsync()
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

        var article = await _contentDefinitionManager.GetTypeDefinitionAsync(KnowledgeBaseConstants.ArticleType);
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

            await _contentDefinitionManager.AlterTypeDefinitionAsync(KnowledgeBaseConstants.ArticleType, type => type
                .WithPart(KnowledgeBaseConstants.ArticleType, part => part.WithPosition("2.5")));
        }

        ShellScope.AddDeferredTask(scope => KnowledgeCategoryTerms.CreateAndConvertAsync(scope.ServiceProvider, taxonomyId));
    }
}
