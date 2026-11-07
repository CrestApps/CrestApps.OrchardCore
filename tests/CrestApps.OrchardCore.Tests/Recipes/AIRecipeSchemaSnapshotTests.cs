using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Tests.Architecture;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Recipes;

/// <summary>
/// Pins the JSON schema of the AI recipe steps and AI site settings, which moved from Recipes.Core into the AI
/// libraries so each AI module owns its schemas. The types are found by name in any loaded assembly, so the snapshot
/// holds wherever they live; a schema that changes on purpose updates <c>AIRecipeSchemas.approved.json</c>.
/// </summary>
public sealed class AIRecipeSchemaSnapshotTests
{
    private static readonly string[] _recipeSteps =
    [
        "A2AConnectionRecipeStep",
        "AIDataSourceRecipeStep",
        "AIDeploymentRecipeStep",
        "AIPersonaRecipeStep",
        "AIProfileRecipeStep",
        "AIProfileTemplateRecipeStep",
        "AIProviderConnectionsRecipeStep",
        "AIToolInstanceRecipeStep",
        "CreateAIProfileFromTemplateRecipeStep",
        "DeleteAIDeploymentsRecipeStep",
        "McpConnectionRecipeStep",
        "McpPromptRecipeStep",
        "McpResourceRecipeStep",
    ];

    private static readonly string[] _siteSettings =
    [
        "AIVisitorIdentityOptionsSchema",
        "PromptSecurityOptionsSchema",
    ];

    [Fact]
    public async Task AIRecipeSchemas_ShouldMatchTheApprovedSnapshot()
    {
        // Arrange
        var services = new ServiceCollection().AddOptions().BuildServiceProvider();
        var types = LoadTypes();
        var snapshot = new JsonObject();

        // Act
        foreach (var name in _recipeSteps)
        {
            var step = (IRecipeStep)ActivatorUtilities.CreateInstance(services, types[name]);
            snapshot[name] = JsonSerializer.SerializeToNode(await step.GetSchemaAsync(TestContext.Current.CancellationToken));
        }

        foreach (var name in _siteSettings)
        {
            var schema = (ISiteSettingsSchemaDefinition)ActivatorUtilities.CreateInstance(services, types[name]);
            snapshot[name] = JsonSerializer.SerializeToNode((await schema.GetSchemaAsync(TestContext.Current.CancellationToken)).Build());
        }

        var actual = snapshot.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).ReplaceLineEndings("\n");

        // Assert
        var approvedPath = Path.Combine(ProjectReferenceRulesTests.FindRepositoryRoot(), "tests", "CrestApps.OrchardCore.Tests", "Recipes", "AIRecipeSchemas.approved.json");
        var receivedPath = approvedPath.Replace(".approved.json", ".received.json", StringComparison.Ordinal);
        var approved = File.Exists(approvedPath) ? File.ReadAllText(approvedPath).ReplaceLineEndings("\n") : null;

        if (!string.Equals(approved, actual, StringComparison.Ordinal))
        {
            File.WriteAllText(receivedPath, actual, new UTF8Encoding(false));
        }

        Assert.True(string.Equals(approved, actual, StringComparison.Ordinal), $"The AI recipe schemas changed. Compare {receivedPath} with {approvedPath}.");
    }

    private static Dictionary<string, Type> LoadTypes()
    {
        var wanted = _recipeSteps.Concat(_siteSettings).ToHashSet(StringComparer.Ordinal);
        var types = new Dictionary<string, Type>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(AppContext.BaseDirectory, "CrestApps.OrchardCore.*.dll"))
        {
            Assembly assembly;

            try
            {
                assembly = Assembly.LoadFrom(file);
            }
            catch (BadImageFormatException)
            {
                continue;
            }

            Type[] exported;

            try
            {
                exported = assembly.GetExportedTypes();
            }
            catch (Exception ex) when (ex is ReflectionTypeLoadException or FileNotFoundException or FileLoadException)
            {
                continue;
            }

            foreach (var type in exported)
            {
                if (wanted.Contains(type.Name) && !type.IsAbstract &&
                    (typeof(IRecipeStep).IsAssignableFrom(type) || typeof(ISiteSettingsSchemaDefinition).IsAssignableFrom(type)))
                {
                    types[type.Name] = type;
                }
            }
        }

        Assert.Equal(wanted.Order(StringComparer.Ordinal), types.Keys.Order(StringComparer.Ordinal));

        return types;
    }
}
