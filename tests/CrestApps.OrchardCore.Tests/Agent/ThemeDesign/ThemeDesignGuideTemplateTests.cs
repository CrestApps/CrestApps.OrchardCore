using Fluid;

namespace CrestApps.OrchardCore.Tests.Agent.ThemeDesign;

public sealed class ThemeDesignGuideTemplateTests
{
    [Fact]
    public async Task Guide_RendersItsLiquidExamplesAsText()
    {
        // Arrange: prompt templates are rendered through Liquid, so the guide's own examples must come out as written.
        var path = Path.Combine(FindRepositoryRoot(), "src", "Modules", "CrestApps.OrchardCore.AI.Agent", "Templates", "Prompts", "theme-design-guide.md");
        var body = File.ReadAllText(path).Split("---", 3)[2];
        var parser = new FluidParser();

        // Act
        var parsed = parser.TryParse(body, out var template, out var error);
        var output = parsed ? await template.RenderAsync(new TemplateContext()) : null;

        // Assert
        Assert.True(parsed, error);
        Assert.Contains("{{ Model.ContentItem.DisplayText }}", output);
        Assert.Contains("{% resources type: \"FootScript\" %}", output);
        Assert.Contains("{% if Request.Path == \"/\" %}", output);
        Assert.DoesNotContain("{% raw %}", output);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
