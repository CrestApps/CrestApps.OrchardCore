using System.Security.Claims;
using CrestApps.OrchardCore.KnowledgeBase.Indexes;
using CrestApps.OrchardCore.KnowledgeBase.Models;
using CrestApps.OrchardCore.KnowledgeBase.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.KnowledgeBase.AI;

/// <summary>
/// Reads one published help article the user may open, in full, as its page shows it: the published version, with
/// <c>ViewContent</c> on it. A draft, an item that is not an article and an article the user may not open get the same
/// answer as an id that does not exist, so nothing tells a restricted article exists.
/// </summary>
internal sealed class GetKnowledgeArticleTool : KnowledgeBaseAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string TheName = "getKnowledgeArticle";

    /// <summary>
    /// The most characters of the body returned.
    /// </summary>
    public const int MaxBodyLength = 20000;

    /// <summary>
    /// The answer for an id that is not a published article the user may open.
    /// </summary>
    public const string NotFoundMessage = "No published article the user may read has that id. Find articles with searchKnowledgeBase.";

    /// <summary>
    /// Initializes a new instance of the <see cref="GetKnowledgeArticleTool"/> class.
    /// </summary>
    public GetKnowledgeArticleTool()
        : base("""
            {
              "type": "object",
              "properties": {
                "articleId": {
                  "type": "string",
                  "description": "The article's id, as searchKnowledgeBase returns it."
                }
              },
              "required": [ "articleId" ],
              "additionalProperties": false
            }
            """)
    {
    }

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Reads one published help article the user may read, in full: title, summary, category, link and body. Use it after searchKnowledgeBase when the passage is not enough to answer.";

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (ReadString(arguments, "articleId") is not { } articleId)
        {
            return "Nothing was read: give the article's id (articleId), as searchKnowledgeBase returns it.";
        }

        var article = await services.GetRequiredService<IContentManager>().GetAsync(articleId, VersionOptions.Published);

        if (article is null
            || !article.TryGet<KnowledgeArticlePart>(out var part)
            || !await CanViewAsync(services, caller, article))
        {
            return NotFoundMessage;
        }

        var tree = await services.GetRequiredService<KnowledgeBaseCategories>().GetTreeAsync();
        var body = KnowledgeArticleProjection.Cut(KnowledgeArticleProjection.GetBody(article), MaxBodyLength, out var bodyTruncated);

        return Json(new
        {
            id = article.ContentItemId,
            title = article.DisplayText,
            summary = part.Summary,
            category = KnowledgeArticleProjection.GetCategory(tree, KnowledgeArticleIndexProvider.CategoryOf(article)),
            link = KnowledgeArticleProjection.GetLink(services, await KnowledgeArticleProjection.GetLinkBaseAsync(services), article),
            publishedAt = await ToLocalAsync(services, article.PublishedUtc),
            helpfulCount = part.HelpfulCount,
            notHelpfulCount = part.NotHelpfulCount,
            body,
            bodyTruncated,
            dataNote = DataNote,
        });
    }
}
