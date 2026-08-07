using CrestApps.OrchardCore.Recipes.Core.Schemas;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Indexing;
using OrchardCore.Localization;
using OrchardCore.Security.Services;

namespace CrestApps.OrchardCore.Recipes.Core.Services;

/// <summary>
/// Builds a <see cref="RecipeSchemaExamples"/> snapshot from the services available on the current tenant. Each
/// source is resolved optionally so the snapshot degrades gracefully when a feature is not enabled.
/// </summary>
public sealed class RecipeSchemaExampleService : IRecipeSchemaExampleService
{
    private readonly IServiceProvider _serviceProvider;

    private RecipeSchemaExamples _cachedExamples;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecipeSchemaExampleService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve the optional example sources.</param>
    public RecipeSchemaExampleService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public async ValueTask<RecipeSchemaExamples> GetExamplesAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedExamples is not null)
        {
            return _cachedExamples;
        }

        _cachedExamples = new RecipeSchemaExamples
        {
            ContentTypeNames = await GetContentTypeNamesAsync(),
            ContentPartNames = await GetContentPartNamesAsync(),
            CultureNames = await GetCultureNamesAsync(),
            RoleNames = await GetRoleNamesAsync(),
            IndexProfileNames = await GetIndexProfileNamesAsync(),
        };

        return _cachedExamples;
    }

    private async ValueTask<IReadOnlyList<string>> GetContentTypeNamesAsync()
    {
        var contentDefinitionManager = _serviceProvider.GetService<IContentDefinitionManager>();

        if (contentDefinitionManager is null)
        {
            return [];
        }

        var definitions = await contentDefinitionManager.ListTypeDefinitionsAsync();

        return definitions
            .Select(definition => definition.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    private async ValueTask<IReadOnlyList<string>> GetContentPartNamesAsync()
    {
        var contentDefinitionManager = _serviceProvider.GetService<IContentDefinitionManager>();

        if (contentDefinitionManager is null)
        {
            return [];
        }

        var definitions = await contentDefinitionManager.ListPartDefinitionsAsync();

        return definitions
            .Select(definition => definition.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    private async ValueTask<IReadOnlyList<string>> GetCultureNamesAsync()
    {
        var localizationService = _serviceProvider.GetService<ILocalizationService>();

        if (localizationService is null)
        {
            return [];
        }

        var cultures = await localizationService.GetSupportedCulturesAsync();

        return cultures
            .Where(culture => !string.IsNullOrWhiteSpace(culture))
            .OrderBy(culture => culture, StringComparer.Ordinal)
            .ToArray();
    }

    private async ValueTask<IReadOnlyList<string>> GetRoleNamesAsync()
    {
        var roleService = _serviceProvider.GetService<IRoleService>();

        if (roleService is null)
        {
            return [];
        }

        var roles = await roleService.GetRolesAsync();

        return roles
            .Select(role => role.RoleName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }

    private async ValueTask<IReadOnlyList<string>> GetIndexProfileNamesAsync()
    {
        var indexProfileStore = _serviceProvider.GetService<IIndexProfileStore>();

        if (indexProfileStore is null)
        {
            return [];
        }

        var profiles = await indexProfileStore.GetAllAsync();

        return profiles
            .Select(profile => profile.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }
}
