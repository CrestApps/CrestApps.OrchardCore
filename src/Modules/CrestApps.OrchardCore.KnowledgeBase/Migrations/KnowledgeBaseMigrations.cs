using CrestApps.OrchardCore.KnowledgeBase.Indexes;
using CrestApps.OrchardCore.KnowledgeBase.Models;
using OrchardCore.Autoroute.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Markdown.Models;
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
            .WithDescription("Makes a content item a help article: who may read it, its category and summary, and what readers said about it."));

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
            .Column<string>("Category", column => column.WithLength(100))
            .Column<int>("Audience")
            .Column<DateTime>("PublishedUtc", column => column.Nullable())
            .Column<int>("HelpfulCount", column => column.Nullable()));

        await SchemaBuilder.AlterIndexTableAsync<KnowledgeArticleIndex>(table =>
        {
            table.CreateIndex("IDX_KnowledgeArticleIndex_Category", "Audience", "Category", "DocumentId");
            table.CreateIndex("IDX_KnowledgeArticleIndex_ContentItemId", "ContentItemId");
        });

        return 1;
    }
}
