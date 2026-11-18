using CrestApps.OrchardCore.KnowledgeBase;
using CrestApps.OrchardCore.KnowledgeBase.Navigation;
using CrestApps.OrchardCore.KnowledgeBase.Services;
using CrestApps.OrchardCore.Tests.Modules.Accounts;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Contents;
using OrchardCore.Navigation;
using OrchardCore.Taxonomies.Settings;

namespace CrestApps.OrchardCore.Tests.KnowledgeBase;

/// <summary>
/// The Knowledge Base was a part of the Service Desk menu, though a site may have a knowledge base without a service desk
/// or a service desk without one: it has its own menu, with the articles, the categories, the feedback report and the
/// help center.
/// </summary>
public sealed class KnowledgeBaseAdminMenuTests
{
    [Fact]
    public async Task TheKnowledgeBase_HasItsOwnMenu_WithTheArticlesTheCategoriesAndTheHelpCenter()
    {
        // Arrange
        var definitions = new InMemoryContentDefinitionManager();
        await definitions.StorePartDefinitionAsync(new ContentPartDefinitionBuilder()
            .Named(KnowledgeBaseConstants.ArticleType)
            .WithField(KnowledgeBaseConstants.CategoryField, field => field.OfType("TaxonomyField").WithSettings(new TaxonomyFieldSettings { TaxonomyContentItemId = "taxonomy" }))
            .Build());
        await definitions.StoreTypeDefinitionAsync(new ContentTypeDefinitionBuilder()
            .WithName(KnowledgeBaseConstants.ArticleType)
            .WithPart(KnowledgeBaseConstants.ArticleType, (await definitions.LoadPartDefinitionAsync(KnowledgeBaseConstants.ArticleType)), part => part.WithPosition("4"))
            .Build());
        var builder = new NavigationBuilder();

        // Act
        await new KnowledgeBaseAdminMenu(new KnowledgeBaseCategories(definitions, Mock.Of<IContentManager>()), new PassThroughLocalizer<KnowledgeBaseAdminMenu>())
            .BuildNavigationAsync(NavigationConstants.AdminId, builder);

        // Assert: its own top menu, not under Service Desk.
        var knowledgeBase = Assert.Single(builder.Build());
        Assert.Equal((KnowledgeBaseAdminMenu.MenuId, "Knowledge Base"), (knowledgeBase.Id, knowledgeBase.Text.Value));
        Assert.Contains(knowledgeBase.Permissions, permission => permission.Name == CommonPermissions.ListContent.Name);

        var items = knowledgeBase.Items.OrderBy(item => item.Position, StringComparer.Ordinal).ToList();
        Assert.Equal(["knowledgeArticles", "knowledgeBaseCategories", "knowledgeBaseHelpCenter"], items.Select(item => item.Id));
        Assert.Equal(("OrchardCore.Contents", KnowledgeBaseConstants.ArticleType), ((string)items[0].RouteValues["area"], (string)items[0].RouteValues["contentTypeId"]));
        Assert.Equal("taxonomy", (string)items[1].RouteValues["contentItemId"]);
        Assert.Equal("~/help", items[2].Url);
        Assert.All(items, item => Assert.Contains(item.Permissions, permission => permission.Name == CommonPermissions.ListContent.Name));
    }

    [Fact]
    public async Task TheFeedbackReport_JoinsTheKnowledgeBaseMenu_ForThoseWhoMayViewIt()
    {
        // Arrange
        var builder = new NavigationBuilder();

        // Act
        await new KnowledgeBaseFeedbackAdminMenu(new PassThroughLocalizer<KnowledgeBaseFeedbackAdminMenu>()).BuildNavigationAsync(NavigationConstants.AdminId, builder);

        // Assert
        var knowledgeBase = Assert.Single(builder.Build());
        Assert.Equal(KnowledgeBaseAdminMenu.MenuId, knowledgeBase.Id);
        var feedback = Assert.Single(knowledgeBase.Items);
        Assert.Equal(("knowledgeBaseFeedback", "knowledge-base-feedback"), (feedback.Id, (string)feedback.RouteValues["id"]));
        Assert.Contains(feedback.Permissions, permission => permission.Name == KnowledgeBasePermissions.ViewFeedback.Name);
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
