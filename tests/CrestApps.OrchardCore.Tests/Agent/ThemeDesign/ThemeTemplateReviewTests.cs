using CrestApps.OrchardCore.AI.Agent.ThemeDesign;

namespace CrestApps.OrchardCore.Tests.Agent.ThemeDesign;

public sealed class ThemeTemplateReviewTests
{
    private const string ThemeLayout =
        """
        {% style name:"font-awesome", version:"7", at:"Head" %}
        {% style name:"theblogtheme" %}
        {% script name:"theblogtheme", at:"Foot" %}
        """;

    [Fact]
    public void GetResourceNames_ReadsLiquidAndRazorResourceTags()
    {
        // Act
        var names = ThemeTemplateReview.GetResourceNames(ThemeLayout + "<script asp-name=\"bootstrap\" at=\"Foot\"></script>");

        // Assert
        Assert.Equal(["font-awesome", "theblogtheme", "bootstrap"], names);
    }

    [Fact]
    public void GetDroppedResources_NamesWhatTheNewLayoutNoLongerLoads()
    {
        // Arrange
        const string Layout = "{% style name: \"TheBlogTheme\" %}{% style name:\"site-theme\" %}";

        // Act
        var dropped = ThemeTemplateReview.GetDroppedResources(ThemeLayout, Layout);

        // Assert
        Assert.Equal(["font-awesome"], dropped);
    }

    [Fact]
    public void GetLiquidTag_QuotesTheThemesOwnTagWithItsArguments()
    {
        // Act & Assert
        Assert.Equal("{% style name:\"font-awesome\", version:\"7\", at:\"Head\" %}", ThemeTemplateReview.GetLiquidTag(ThemeLayout, "font-awesome"));
        Assert.Equal("{% script name: \"bootstrap\", at: \"Foot\" %}", ThemeTemplateReview.GetLiquidTag("<script asp-name=\"bootstrap\"></script>", "bootstrap"));
    }

    [Fact]
    public void IsNewTheme_IsTrueOnlyWhenTheDraftLayoutDropsTheThemesResources()
    {
        // Act & Assert
        Assert.True(ThemeTemplateReview.IsNewTheme(ThemeLayout, "{% style name: \"bootstrap\" %}"));
        Assert.False(ThemeTemplateReview.IsNewTheme(ThemeLayout, ThemeLayout + "{% style name: \"site-theme\" %}"));
        Assert.False(ThemeTemplateReview.IsNewTheme(ThemeLayout, null));
    }

    [Fact]
    public void GetNewThemeAdvice_NamesTheThemesTemplatesAndAThinStylesheet()
    {
        // Act
        var advice = ThemeTemplateReview.GetNewThemeAdvice(
            [
                ("content__blog", "Areas/TheBlogTheme/Views/Content-Blog.liquid"),
                ("Layout", "theme draft"),
                ("Content_Summary__BlogPost", "theme draft"),
            ],
            siteStylesheetBytes: 700);

        // Assert
        Assert.Equal(2, advice.Count);
        Assert.Contains("content__blog (Areas/TheBlogTheme/Views/Content-Blog.liquid)", advice[0]);
        Assert.DoesNotContain("Content_Summary__BlogPost", advice[0]);
        Assert.Contains("700 bytes", advice[1]);
    }

    [Fact]
    public void GetNewThemeAdvice_ADoneThemeHasNothingLeft()
        => Assert.Empty(ThemeTemplateReview.GetNewThemeAdvice([("content__blog", "theme draft")], siteStylesheetBytes: 9000));

    [Fact]
    public void CountInlineStyles_CountsStyleAttributesOnly()
        => Assert.Equal(2, ThemeTemplateReview.CountInlineStyles("<div style=\"a\"><p class=\"x\" style='b'>{% style name: \"x\" %}</p></div>"));

    [Theory]
    [InlineData("Layout", true)]
    [InlineData("Layout__Home", true)]
    [InlineData("Content", false)]
    public void IsLayout_RecognizesTheLayoutAndItsAlternates(string name, bool expected)
        => Assert.Equal(expected, ThemeTemplateReview.IsLayout(name));
}
