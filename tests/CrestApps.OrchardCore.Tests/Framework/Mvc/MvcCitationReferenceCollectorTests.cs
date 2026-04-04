using CrestApps.AI.Models;
using CrestApps.AI.Orchestration;
using CrestApps.AI.Profiles;
using CrestApps.AI.Services;
using CrestApps.Infrastructure.Indexing;
using CrestApps.Mvc.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Framework.Mvc;

public sealed class MvcCitationReferenceCollectorTests
{
    [Fact]
    public void CollectPreemptiveReferences_ShouldResolveArticleCitationLinks()
    {
        var collector = CreateCollector();
        var context = new OrchestrationContext();
        var references = new Dictionary<string, AICompletionReference>();
        var contentItemIds = new HashSet<string>();

        context.Properties["DataSourceReferences"] = new Dictionary<string, AICompletionReference>
        {
            ["[doc:1]"] = new()
            {
                Index = 1,
                ReferenceId = "article-1",
                ReferenceType = IndexProfileTypes.Articles,
                Title = "Intro to RAG",
            },
        };

        collector.CollectPreemptiveReferences(context, references, contentItemIds);

        var reference = Assert.Single(references);
        Assert.Equal("/articles/article-1", reference.Value.Link);
        Assert.Contains("article-1", contentItemIds);
    }

    [Fact]
    public void CollectToolReferences_ShouldResolveNewArticleLinks()
    {
        var collector = CreateCollector();
        var references = new Dictionary<string, AICompletionReference>();
        var contentItemIds = new HashSet<string>();

        using var scope = AIInvocationScope.Begin();
        scope.Context.ToolReferences["[doc:2]"] = new AICompletionReference
        {
            Index = 2,
            ReferenceId = "article-2",
            ReferenceType = IndexProfileTypes.Articles,
            Title = "Embeddings",
        };

        var added = collector.CollectToolReferences(references, contentItemIds);

        Assert.True(added);
        var reference = Assert.Single(references);
        Assert.Equal("/articles/article-2", reference.Value.Link);
        Assert.Contains("article-2", contentItemIds);
    }

    private static MvcCitationReferenceCollector CreateCollector()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IAIReferenceLinkResolver, TestArticleResolver>(IndexProfileTypes.Articles);
        services.AddSingleton<CompositeAIReferenceLinkResolver>();

        var serviceProvider = services.BuildServiceProvider();

        return new MvcCitationReferenceCollector(serviceProvider.GetRequiredService<CompositeAIReferenceLinkResolver>());
    }

    private sealed class TestArticleResolver : IAIReferenceLinkResolver
    {
        public string ResolveLink(string referenceId, IDictionary<string, object> metadata)
            => $"/articles/{referenceId}";
    }
}
