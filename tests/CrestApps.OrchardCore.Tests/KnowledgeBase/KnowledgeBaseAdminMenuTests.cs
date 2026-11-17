using CrestApps.OrchardCore.KnowledgeBase;
using CrestApps.OrchardCore.KnowledgeBase.Navigation;
using Microsoft.Extensions.Localization;
using OrchardCore.Contents;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Tests.KnowledgeBase;

/// <summary>
/// The knowledge articles were reached only through Content &gt; New &gt; Knowledge Article, so nobody found them: the
/// Knowledge Base module adds them, and the help center, to the Service Desk menu.
/// </summary>
public sealed class KnowledgeBaseAdminMenuTests
{
    [Fact]
    public async Task TheArticlesAndTheHelpCenter_AreAKnowledgeBaseItemOfTheServiceDeskMenu()
    {
        // Arrange
        var builder = new NavigationBuilder();

        // Act
        await new KnowledgeBaseAdminMenu(new PassThroughLocalizer<KnowledgeBaseAdminMenu>()).BuildNavigationAsync(NavigationConstants.AdminId, builder);

        // Assert: the same Service Desk menu as the tickets', with Knowledge Base after Boards (1.5) and before Desks (2).
        var serviceDesk = Assert.Single(builder.Build());
        Assert.Equal(("serviceDesk", "Service Desk"), (serviceDesk.Id, serviceDesk.Text.Value));
        var knowledgeBase = Assert.Single(serviceDesk.Items);
        Assert.Equal(("knowledgeBase", "Knowledge Base"), (knowledgeBase.Id, knowledgeBase.Text.Value));
        Assert.True(string.CompareOrdinal(knowledgeBase.Position, new LocalizedString("Boards", "Boards").PrefixPosition("1.5")) > 0);
        Assert.True(string.CompareOrdinal(knowledgeBase.Position, new LocalizedString("Desks", "Desks").PrefixPosition("2")) < 0);
        Assert.Contains(knowledgeBase.Permissions, permission => permission.Name == CommonPermissions.ListContent.Name);

        var items = knowledgeBase.Items.OrderBy(item => item.Position, StringComparer.Ordinal).ToList();
        Assert.Equal(["knowledgeArticles", "knowledgeBaseHelpCenter"], items.Select(item => item.Id));
        Assert.Equal(("OrchardCore.Contents", KnowledgeBaseConstants.ArticleType), ((string)items[0].RouteValues["area"], (string)items[0].RouteValues["contentTypeId"]));
        Assert.Equal("~/help", items[1].Url);
        Assert.All(items, item => Assert.Contains(item.Permissions, permission => permission.Name == CommonPermissions.ListContent.Name));
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
