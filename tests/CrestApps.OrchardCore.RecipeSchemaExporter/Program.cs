using System.Text.Json;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas;
using CrestApps.OrchardCore.Recipes.Core.Services;
using Json.Schema;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;
using Moq;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Recipes.Services;
using OrchardCore.Security.Permissions;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.RecipeSchemaExporter;

internal sealed class Program
{
    private const string _agentSkillsRepositoryName = "CrestApps.AgentSkills";
    private const string _agentSkillsRelativePath = @"CrestApps.AgentSkills\src\CrestApps.AgentSkills\orchardcore\orchardcore-recipes\references\recipe-schemas";

    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true,
    };

    private static string[] _featureIds = [];
    private static string[] _themeIds = [];

    private static readonly string[] _fieldTypeNames =
    [
        "BooleanField",
        "ContentPickerField",
        "DateField",
        "DateTimeField",
        "HtmlField",
        "LinkField",
        "LocalizationSetContentPickerField",
        "MediaField",
        "MultiTextField",
        "NumericField",
        "TaxonomyField",
        "TextField",
        "TimeField",
        "UserPickerField",
        "YoutubeField",
    ];

    public static async Task<int> Main(string[] args)
    {
        var repositoryRoot = FindRepositoryRoot();
        _featureIds = ManifestScanner.DiscoverFeatureIds(repositoryRoot);
        _themeIds = ManifestScanner.DiscoverThemeIds(repositoryRoot);

        Console.WriteLine($"Discovered {_featureIds.Length} feature IDs and {_themeIds.Length} theme IDs.");

        var outputPath = ResolveOutputPath(args, repositoryRoot);
        var recipeSteps = CreateRecipeSteps()
            .OrderBy(step => step.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Directory.CreateDirectory(outputPath);
        ClearGeneratedArtifacts(outputPath);

        using var memoryCache = new NoOpMemoryCache();
        var schemaService = new RecipeSchemaService([], recipeSteps, memoryCache);
        var indexEntries = new List<RecipeSchemaIndexEntry>();

        foreach (var recipeStep in recipeSteps)
        {
            var stepSchema = await recipeStep.GetSchemaAsync();
            var fileName = GetSchemaFileName(recipeStep.Name);
            var filePath = Path.Combine(outputPath, fileName);

            await WriteSchemaAsync(filePath, stepSchema);
            indexEntries.Add(new RecipeSchemaIndexEntry(recipeStep.Name, fileName));
        }

        await WriteSchemaAsync(
            Path.Combine(outputPath, "recipe.schema.json"),
            await schemaService.GetRecipeSchemaAsync());

        var indexPath = Path.Combine(outputPath, "index.json");
        var indexDocument = new RecipeSchemaIndexDocument(
            DateTimeOffset.UtcNow,
            "recipe.schema.json",
            indexEntries);

        await File.WriteAllTextAsync(
            indexPath,
            JsonSerializer.Serialize(indexDocument, _jsonSerializerOptions));

        Console.WriteLine($"Exported {indexEntries.Count} step schemas to '{outputPath}'.");
        Console.WriteLine("Generated files:");
        Console.WriteLine(" - recipe.schema.json");
        Console.WriteLine(" - index.json");

        foreach (var entry in indexEntries)
        {
            Console.WriteLine($" - {entry.File}");
        }

        return 0;
    }

    private static string ResolveOutputPath(string[] args, string repositoryRoot)
    {
        if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        {
            return Path.GetFullPath(args[0]);
        }

        return GetDefaultOutputPath(repositoryRoot);
    }

    private static string GetDefaultOutputPath(string repositoryRoot)
    {
        var parentDirectory = Directory.GetParent(repositoryRoot)
            ?? throw new DirectoryNotFoundException($"Could not determine the parent directory for '{repositoryRoot}'.");
        var agentSkillsRoot = Path.Combine(parentDirectory.FullName, _agentSkillsRepositoryName);

        if (!Directory.Exists(agentSkillsRoot))
        {
            var fallbackPath = Path.Combine(repositoryRoot, "artifacts", "recipe-schemas");
            Console.WriteLine($"Warning: Could not locate the sibling '{_agentSkillsRepositoryName}' repository.");
            Console.WriteLine($"  Expected: '{agentSkillsRoot}'");
            Console.WriteLine($"  Falling back to local output: '{fallbackPath}'");
            Console.WriteLine();

            return fallbackPath;
        }

        return Path.Combine(parentDirectory.FullName, _agentSkillsRelativePath);
    }

    private static string FindRepositoryRoot()
    {
        var candidateDirectories = new[]
        {
            AppContext.BaseDirectory,
            Environment.CurrentDirectory,
        }
            .Select(path => new DirectoryInfo(path))
            .DistinctBy(directory => directory.FullName)
            .ToArray();

        foreach (var candidateDirectory in candidateDirectories)
        {
            for (var current = candidateDirectory; current is not null; current = current.Parent)
            {
                if (IsRepositoryRoot(current))
                {
                    return current.FullName;
                }
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the CrestApps.OrchardCore repository root. " +
            "Expected a parent directory containing global.json, NuGet.config, CrestApps.OrchardCore.sln, or CrestApps.OrchardCore.slnx.");
    }

    private static bool IsRepositoryRoot(DirectoryInfo directory)
        => File.Exists(Path.Combine(directory.FullName, "global.json")) ||
            File.Exists(Path.Combine(directory.FullName, "NuGet.config")) ||
                File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.sln")) ||
                File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx"));

    private static void ClearGeneratedArtifacts(string outputPath)
    {
        foreach (var filePath in Directory.EnumerateFiles(outputPath, "*.schema.json", SearchOption.TopDirectoryOnly))
        {
            File.Delete(filePath);
        }

        var indexPath = Path.Combine(outputPath, "index.json");
        if (File.Exists(indexPath))
        {
            File.Delete(indexPath);
        }
    }

    private static async Task WriteSchemaAsync(string filePath, JsonSchema schema)
    {
        await File.WriteAllTextAsync(
            filePath,
            JsonSerializer.Serialize(schema, _jsonSerializerOptions));
    }

    private static string GetSchemaFileName(string stepName)
    {
        var invalidFileNameChars = Path.GetInvalidFileNameChars();
        var safeName = new string(stepName.Select(character => invalidFileNameChars.Contains(character) ? '-' : character).ToArray());
        return $"{safeName}.schema.json";
    }

    private static IRecipeStep[] CreateRecipeSteps()
    {
        var schemaDefinitions = CreateContentDefinitionSchemaDefinitions();
        var partNames = schemaDefinitions
            .Select(definition => definition.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return typeof(IRecipeStep).Assembly.ExportedTypes
            .Where(type =>
                typeof(IRecipeStep).IsAssignableFrom(type) &&
                    type is { IsAbstract: false, IsInterface: false })
                    .OrderBy(type => type.Name, StringComparer.Ordinal)
                    .Select(type => CreateRecipeStep(type, schemaDefinitions, partNames))
                    .ToArray();
    }

    private static IContentDefinitionSchemaDefinition[] CreateContentDefinitionSchemaDefinitions()
    {
        return typeof(IContentDefinitionSchemaDefinition).Assembly.ExportedTypes
            .Where(type =>
                typeof(IContentDefinitionSchemaDefinition).IsAssignableFrom(type) &&
                    type is { IsAbstract: false, IsInterface: false })
                    .OrderBy(type => type.Name, StringComparer.Ordinal)
                    .Select(type => (IContentDefinitionSchemaDefinition)Activator.CreateInstance(type)!)
                    .ToArray();
    }

    private static ISiteSettingsSchemaDefinition[] CreateSiteSettingsSchemaDefinitions()
    {
        return typeof(ISiteSettingsSchemaDefinition).Assembly.ExportedTypes
            .Where(type =>
                typeof(ISiteSettingsSchemaDefinition).IsAssignableFrom(type) &&
                    type is { IsAbstract: false, IsInterface: false })
                    .OrderBy(type => type.Name, StringComparer.Ordinal)
                    .Select(type => (ISiteSettingsSchemaDefinition)Activator.CreateInstance(type)!)
                    .ToArray();
    }

    private static IRecipeStep CreateRecipeStep(
        Type stepType,
        IReadOnlyList<IContentDefinitionSchemaDefinition> schemaDefinitions,
        IReadOnlyList<string> partNames)
    {
        if (stepType == typeof(SettingsRecipeStep))
        {
            return new SettingsRecipeStep(CreateSiteSettingsSchemaDefinitions());
        }

        if (stepType == typeof(ContentDefinitionRecipeStep))
        {
            return new ContentDefinitionRecipeStep(
                schemaDefinitions,
                new StubContentSchemaProvider(partNames, _fieldTypeNames));
        }

        if (stepType == typeof(ContentRecipeStep))
        {
            return new ContentRecipeStep(CreateContentDefinitionManager());
        }

        if (stepType == typeof(ReplaceContentDefinitionRecipeStep))
        {
            return new ReplaceContentDefinitionRecipeStep(
                schemaDefinitions,
                new StubContentSchemaProvider(partNames, _fieldTypeNames));
        }

        if (stepType == typeof(FeatureRecipeStep))
        {
            return new FeatureRecipeStep(CreateShellFeaturesManager());
        }

        if (stepType == typeof(RecipesRecipeStep))
        {
            return new RecipesRecipeStep(CreateRecipeHarvesters(), CreateShellFeaturesManager());
        }

        if (stepType == typeof(RolesRecipeStep))
        {
            return new RolesRecipeStep(CreatePermissionService());
        }

        if (stepType == typeof(ThemesRecipeStep))
        {
            return new ThemesRecipeStep(new StubFeatureSchemaProvider());
        }

        if (stepType == typeof(WorkflowTypeRecipeStep))
        {
            return new WorkflowTypeRecipeStep(CreateActivityLibrary());
        }

        return (IRecipeStep)Activator.CreateInstance(stepType)!;
    }

    private static IContentDefinitionManager CreateContentDefinitionManager()
    {
        var manager = new Mock<IContentDefinitionManager>();
        manager.Setup(service => service.ListTypeDefinitionsAsync())
            .ReturnsAsync(Array.Empty<ContentTypeDefinition>());

        return manager.Object;
    }

    private static IActivityLibrary CreateActivityLibrary()
    {
        var startActivity = new Mock<IEvent>();
        startActivity.SetupGet(activity => activity.Name).Returns("HttpRequestEvent");

        var taskActivity = new Mock<ITask>();
        taskActivity.SetupGet(activity => activity.Name).Returns("NotifyTask");

        var library = new Mock<IActivityLibrary>();
        library.Setup(service => service.ListActivities())
            .Returns([startActivity.Object, taskActivity.Object]);

        return library.Object;
    }

    private static IPermissionService CreatePermissionService()
    {
        var permissions = new[]
        {
            new Permission("PermissionA", "Permission A"),
            new Permission("PermissionB", "Permission B"),
        };

        var permissionService = new Mock<IPermissionService>();
        permissionService.Setup(service => service.GetPermissionsAsync())
            .Returns(new ValueTask<IEnumerable<Permission>>(permissions));
        permissionService.Setup(service => service.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((Permission)null);

        return permissionService.Object;
    }

    private static IEnumerable<IRecipeHarvester> CreateRecipeHarvesters()
    {
        var recipeHarvester = new Mock<IRecipeHarvester>();
        recipeHarvester.Setup(service => service.HarvestRecipesAsync())
            .ReturnsAsync([]);

        return [recipeHarvester.Object];
    }

    private static IShellFeaturesManager CreateShellFeaturesManager()
    {
        var features = _featureIds
            .Select(id =>
            {
                var feature = new Mock<IFeatureInfo>();
                feature.SetupGet(item => item.Id).Returns(id);
                return feature.Object;
            })
            .ToArray();

        var manager = new Mock<IShellFeaturesManager>();
        manager.Setup(service => service.GetAvailableFeaturesAsync())
            .ReturnsAsync(features);

        return manager.Object;
    }

    private sealed class StubContentSchemaProvider(
        IReadOnlyList<string> partNames,
        IReadOnlyList<string> fieldTypeNames) : IContentSchemaProvider
    {
        public Task<IEnumerable<string>> GetFieldTypeNamesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<string>>(fieldTypeNames);

        public Task<IEnumerable<string>> GetPartNamesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<string>>(partNames);
    }

    private sealed class StubFeatureSchemaProvider : IFeatureSchemaProvider
    {
        public Task<IEnumerable<string>> GetFeatureIdsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<string>>(_featureIds);

        public Task<IEnumerable<string>> GetThemeIdsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IEnumerable<string>>(_themeIds);
    }

    private sealed class NoOpMemoryCache : IMemoryCache
    {
        public void Dispose()
        {
        }

        public bool TryGetValue(object key, out object value)
        {
            value = null;
            return false;
        }

        public ICacheEntry CreateEntry(object key)
            => new NoOpCacheEntry(key);

        public void Remove(object key)
        {
        }
    }

    private sealed class NoOpCacheEntry(object key) : ICacheEntry
    {
        public object Key { get; } = key;

        public object Value { get; set; }

        public DateTimeOffset? AbsoluteExpiration { get; set; }

        public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }

        public TimeSpan? SlidingExpiration { get; set; }

        public IList<IChangeToken> ExpirationTokens { get; } = [];

        public IList<PostEvictionCallbackRegistration> PostEvictionCallbacks { get; } = [];

        public CacheItemPriority Priority { get; set; }

        public long? Size { get; set; }

        public void Dispose()
        {
        }
    }

    private sealed record RecipeSchemaIndexDocument(
        DateTimeOffset GeneratedUtc,
        string RootSchema,
        IReadOnlyList<RecipeSchemaIndexEntry> Steps);

    private sealed record RecipeSchemaIndexEntry(string Name, string File);
}
