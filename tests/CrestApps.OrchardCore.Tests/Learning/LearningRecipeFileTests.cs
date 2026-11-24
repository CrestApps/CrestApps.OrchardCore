using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning;

namespace CrestApps.OrchardCore.Tests.Learning;

public sealed class LearningRecipeFileTests
{
    [Fact]
    public void TheCatalogRecipe_IndexesCoursesOnly_SoLessonBodiesNeverReachSearch()
    {
        // Arrange
        var path = FindRepositoryFile(Path.Combine("src", "Modules", "CrestApps.OrchardCore.Learning", "Recipes", "learning-catalog.recipe.json"));

        // Act
        var recipe = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var indexes = recipe["steps"]!.AsArray()
            .OfType<JsonObject>()
            .Where(step => step["name"]?.GetValue<string>() == "CreateOrUpdateIndexProfile")
            .SelectMany(step => step["indexes"]!.AsArray().OfType<JsonObject>())
            .ToArray();

        // Assert
        Assert.NotEmpty(indexes);
        Assert.All(indexes, index =>
        {
            var types = index["IndexedContentTypes"]!.AsArray().Select(type => type!.GetValue<string>()).ToArray();
            Assert.Equal([LearningConstants.ContentTypes.Course], types);
        });
    }

    private static string FindRepositoryFile(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("The file was not found above the test output folder.", relativePath);
    }
}
