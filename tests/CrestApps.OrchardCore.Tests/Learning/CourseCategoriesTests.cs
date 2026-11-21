using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContentFields.Taxonomies;
using CrestApps.OrchardCore.Learning;
using CrestApps.OrchardCore.Learning.Core.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// Course categories: how the feature finds or makes the category field and taxonomy on a site that may already have
/// them, and how a category named by an import is found in the tree or added to it.
/// </summary>
public sealed class CourseCategoriesTests
{
    [Fact]
    public void Setup_OnASiteWithNothing_AddsTheCategoryField_AndANewTaxonomy()
    {
        // Act
        var plan = CourseCategorySetup.Decide([], existingTaxonomyId: null, newTaxonomyId: "new-taxonomy");

        // Assert
        Assert.Equal(LearningConstants.CourseCategories.FieldName, plan.FieldName);
        Assert.Equal("new-taxonomy", plan.TaxonomyId);
        Assert.True(plan.AddField);
        Assert.True(plan.CreateTaxonomy);
        Assert.False(plan.SetFieldTaxonomy);
    }

    [Fact]
    public void Setup_WithAHandMadeCourseCategoriesTaxonomy_UsesIt_InsteadOfMakingAnother()
    {
        // Act
        var plan = CourseCategorySetup.Decide([], existingTaxonomyId: "hand-made", newTaxonomyId: "new-taxonomy");

        // Assert
        Assert.Equal("hand-made", plan.TaxonomyId);
        Assert.True(plan.AddField);
        Assert.False(plan.CreateTaxonomy);
    }

    [Fact]
    public void Setup_WithAHandMadeCategoryField_KeepsTheFieldAndItsTaxonomy()
    {
        // Act
        var plan = CourseCategorySetup.Decide(
            [new CourseCategoryFieldCandidate("Category", "TaxonomyField", "their-taxonomy")],
            existingTaxonomyId: "hand-made",
            newTaxonomyId: "new-taxonomy");

        // Assert
        Assert.Equal("Category", plan.FieldName);
        Assert.Equal("their-taxonomy", plan.TaxonomyId);
        Assert.False(plan.AddField);
        Assert.False(plan.CreateTaxonomy);
        Assert.False(plan.SetFieldTaxonomy);
    }

    [Fact]
    public void Setup_WithAnotherTaxonomyFieldOnTheCourse_UsesThatField()
    {
        // Act
        var plan = CourseCategorySetup.Decide(
            [
                new CourseCategoryFieldCandidate("Cover", "MediaField", null),
                new CourseCategoryFieldCandidate("Topics", "TaxonomyField", "topics-taxonomy"),
            ],
            existingTaxonomyId: null,
            newTaxonomyId: "new-taxonomy");

        // Assert
        Assert.Equal("Topics", plan.FieldName);
        Assert.Equal("topics-taxonomy", plan.TaxonomyId);
        Assert.False(plan.AddField);
        Assert.False(plan.CreateTaxonomy);
    }

    [Fact]
    public void Setup_WithACategoryFieldThatHasNoTaxonomy_PointsItAtTheTaxonomy()
    {
        // Act
        var plan = CourseCategorySetup.Decide(
            [new CourseCategoryFieldCandidate("Category", "TaxonomyField", null)],
            existingTaxonomyId: null,
            newTaxonomyId: "new-taxonomy");

        // Assert
        Assert.Equal("Category", plan.FieldName);
        Assert.Equal("new-taxonomy", plan.TaxonomyId);
        Assert.False(plan.AddField);
        Assert.True(plan.SetFieldTaxonomy);
        Assert.True(plan.CreateTaxonomy);
    }

    [Fact]
    public void Setup_WhenCategoryIsTakenByAFieldOfAnotherKind_AddsTheFieldUnderAnotherName()
    {
        // Act
        var plan = CourseCategorySetup.Decide(
            [new CourseCategoryFieldCandidate("Category", "TextField", null)],
            existingTaxonomyId: null,
            newTaxonomyId: "new-taxonomy");

        // Assert
        Assert.NotEqual("Category", plan.FieldName);
        Assert.True(plan.AddField);
    }

    [Fact]
    public void FindOrAdd_FindsANameAnywhereInTheTree_AndAddsAMissingOneAtTheTop()
    {
        // Arrange
        var taxonomy = Taxonomy(Term("t1", "Onboarding", Term("t2", "30 Day Learning Plan")));

        // Act
        var found = CourseCategoryTerms.FindOrAdd(taxonomy, ["30 day learning plan"], LearningConstants.CourseCategories.TermContentType, out var addedFound);
        var added = CourseCategoryTerms.FindOrAdd(taxonomy, ["Leadership"], LearningConstants.CourseCategories.TermContentType, out var addedNew);
        var tree = TaxonomyTermTree.From(taxonomy);

        // Assert
        Assert.Equal("t2", found);
        Assert.False(addedFound);
        Assert.True(addedNew);
        var node = tree.Find(added);
        Assert.NotNull(node);
        Assert.Equal("Leadership", node.Name);
        Assert.Null(node.ParentId);
        Assert.Equal(LearningConstants.CourseCategories.TermContentType, node.Content["ContentType"]?.GetValue<string>());
        Assert.Equal("Leadership", node.Content["TitlePart"]?["Title"]?.GetValue<string>());
    }

    [Fact]
    public void FindOrAdd_WithAPath_FollowsItFromTheTop_AndAddsTheMissingLevels()
    {
        // Arrange
        var taxonomy = Taxonomy(Term("t1", "RDI Core", Term("t2", "Agent")));

        // Act
        var existing = CourseCategoryTerms.FindOrAdd(taxonomy, ["RDI Core", "Agent"], "Category", out var addedExisting);
        var deeper = CourseCategoryTerms.FindOrAdd(taxonomy, ["RDI Core", "Leadership", "Coaching"], "Category", out var addedDeeper);
        var tree = TaxonomyTermTree.From(taxonomy);

        // Assert
        Assert.Equal("t2", existing);
        Assert.False(addedExisting);
        Assert.True(addedDeeper);
        Assert.Equal("RDI Core > Leadership > Coaching", tree.GetPathText(deeper));
        Assert.Equal(4, tree.Nodes.Count);
    }

    [Fact]
    public void WithDescendants_GivesACategoryAndEveryCategoryUnderIt()
    {
        // Arrange
        var tree = TaxonomyTermTree.From(Taxonomy(
            Term("a", "A", Term("a1", "A1", Term("a11", "A11"))),
            Term("b", "B")));

        // Act
        var ids = CourseCategoryTerms.WithDescendants(tree, "a");
        var unknown = CourseCategoryTerms.WithDescendants(tree, "zzz");

        // Assert
        Assert.Equal(["a", "a1", "a11"], ids);
        Assert.Empty(unknown);
    }

    private static ContentItem Taxonomy(params JsonObject[] terms)
    {
        var taxonomy = new ContentItem { ContentItemId = "taxonomy", ContentType = "Taxonomy", DisplayText = "Course categories" };
        ((JsonObject)taxonomy.Content)["TaxonomyPart"] = new JsonObject
        {
            ["TermContentType"] = "Category",
            ["Terms"] = new JsonArray(terms),
        };

        return taxonomy;
    }

    private static JsonObject Term(string id, string name, params JsonObject[] children)
        => new()
        {
            ["ContentItemId"] = id,
            ["ContentType"] = "Category",
            ["DisplayText"] = name,
            ["TitlePart"] = new JsonObject { ["Title"] = name },
            ["Terms"] = new JsonArray(children),
        };
}
