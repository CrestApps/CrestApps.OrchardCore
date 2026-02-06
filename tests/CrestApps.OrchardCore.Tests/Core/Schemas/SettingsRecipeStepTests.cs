using System.Text.Json;
using CrestApps.OrchardCore.Recipes.Core.Schemas;

namespace CrestApps.OrchardCore.Tests.Core.Schemas;

public sealed class SettingsRecipeStepTests
{
    [Fact]
    public async Task Name_ReturnsSettings()
    {
        var step = new SettingsRecipeStep();

        Assert.Equal("Settings", step.Name);
    }

    [Fact]
    public async Task GetSchemaAsync_ReturnsValidSchema()
    {
        var step = new SettingsRecipeStep();
        var schema = await step.GetSchemaAsync();

        Assert.NotNull(schema);

        var json = JsonSerializer.Serialize(schema);

        Assert.Contains("\"const\":\"settings\"", json);
        Assert.Contains("\"minProperties\":2", json);
    }

    [Fact]
    public async Task GetSchemaAsync_ReturnsCachedInstance()
    {
        var step = new SettingsRecipeStep();
        var first = await step.GetSchemaAsync();
        var second = await step.GetSchemaAsync();

        Assert.Same(first, second);
    }
}
