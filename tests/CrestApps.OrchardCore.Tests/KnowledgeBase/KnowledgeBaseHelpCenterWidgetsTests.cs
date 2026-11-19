using System.Text.Json.Nodes;
using CrestApps.OrchardCore.KnowledgeBase;
using CrestApps.OrchardCore.KnowledgeBase.Indexes;
using CrestApps.OrchardCore.KnowledgeBase.Models;
using CrestApps.OrchardCore.KnowledgeBase.Recipes;
using CrestApps.OrchardCore.KnowledgeBase.Reports;
using CrestApps.OrchardCore.KnowledgeBase.Services;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.KnowledgeBase;

/// <summary>
/// The help center's widgets (its search box, its categories and an article's related articles), the zones they go in
/// on any theme, and the report of readers' answers.
/// </summary>
public sealed class KnowledgeBaseHelpCenterWidgetsTests
{
    [Fact]
    public void RelatedArticles_PutTheCategoryFirst_WithoutTheArticleItself_OrAnArticleTwice()
    {
        // Act
        var related = KnowledgeBaseHelpCenter.Merge(
            "self",
            [Article("self"), Article("c1"), Article("c2")],
            [Article("c2"), Article("s1"), Article("self"), Article("s2")],
            take: 3);

        // Assert
        Assert.Equal(["c1", "c2", "s1"], related.Select(article => article.ContentItemId));
    }

    [Fact]
    public void TheZonesStep_AddsTheHelpCentersZones_KeepingTheSitesOwn_Once()
    {
        // Arrange
        var properties = new JsonObject { ["LayerSettings"] = new JsonObject { ["Zones"] = new JsonArray("Header", "Content", KnowledgeBaseConstants.Zones.Sidebar) } };

        // Act
        var first = KnowledgeBaseZonesRecipeStep.AddZones(properties);
        var second = KnowledgeBaseZonesRecipeStep.AddZones(properties);

        // Assert
        Assert.True(first);
        Assert.False(second);
        Assert.Equal(["Header", "Content", KnowledgeBaseConstants.Zones.Sidebar, KnowledgeBaseConstants.Zones.Top, KnowledgeBaseConstants.Zones.AfterContent], properties["LayerSettings"]!["Zones"]!.AsArray().Select(zone => zone!.GetValue<string>()));
    }

    [Fact]
    public void TheZonesStep_MakesTheLayersSettings_WhenThereAreNone()
    {
        var properties = new JsonObject();

        Assert.True(KnowledgeBaseZonesRecipeStep.AddZones(properties));
        Assert.Equal(3, properties["LayerSettings"]!["Zones"]!.AsArray().Count);
    }

    [Fact]
    public void TheFeedbackReport_ShowsEachArticleAndCategory_AndWhatWasMissing()
    {
        // Arrange: newest first, as the report reads them.
        KnowledgeArticleFeedback[] answers =
        [
            Answer("f1", "a1", "Reset your password", "accounts", helpful: false, comment: "The link expired.", user: "dana"),
            Answer("f2", "a1", "Reset your password", "accounts", helpful: true),
            Answer("f3", "a2", "Download invoices", "billing", helpful: true),
            Answer("f4", "a1", "Reset your password", "accounts", helpful: true),
            Answer("f5", "a3", "Old article", "gone", helpful: false),
        ];
        var names = new Dictionary<string, string> { ["accounts"] = "Accounts", ["billing"] = "Billing" };

        // Act
        var document = KnowledgeBaseFeedbackReport.Build(answers, id => id is not null && names.TryGetValue(id, out var name) ? name : null, new Dictionary<string, string> { ["f1"] = "11/17/2026 9:30 AM" }, new PassThroughLocalizer());

        // Assert
        var summary = document.Sections[0];
        Assert.Equal(["5", "3", "2", "60.0%", "1"], summary.Metrics.Select(metric => metric.Value));

        var byArticle = Assert.Single(document.Sections, section => section.Title == "By article");
        Assert.Equal(
            [
                ["Old article", "No category", "0", "1", "0.0%", "0"],
                ["Reset your password", "Accounts", "2", "1", "66.7%", "1"],
                ["Download invoices", "Billing", "1", "0", "100.0%", "0"],
            ],
            byArticle.Rows.Select(row => row.Cells.ToArray()));

        var byCategory = Assert.Single(document.Sections, section => section.Title == "By category");
        Assert.Equal(["Accounts", "Billing", "No category"], byCategory.Rows.Select(row => row.Cells[0]));

        var comments = Assert.Single(document.Sections, section => section.Title == "What was missing");
        Assert.Equal(["11/17/2026 9:30 AM", "Reset your password", "The link expired.", "dana"], Assert.Single(comments.Rows).Cells);
    }

    [Fact]
    public void TheFeedbackReport_SaysSoWhenNobodyAnswered()
    {
        var document = KnowledgeBaseFeedbackReport.Build([], _ => null, new Dictionary<string, string>(), new PassThroughLocalizer());

        var empty = Assert.Single(document.Sections, section => section.Title == "By article");
        Assert.Empty(empty.Rows);
        Assert.False(string.IsNullOrEmpty(empty.EmptyMessage));
    }

    private static KnowledgeArticleIndex Article(string id)
        => new() { ContentItemId = id, Title = id };

    private static KnowledgeArticleFeedback Answer(string id, string article, string title, string category, bool helpful, string comment = null, string user = null)
        => new() { ItemId = id, ContentItemId = article, Title = title, CategoryId = category, Helpful = helpful, Comment = comment, UserName = user };

    private sealed class PassThroughLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
