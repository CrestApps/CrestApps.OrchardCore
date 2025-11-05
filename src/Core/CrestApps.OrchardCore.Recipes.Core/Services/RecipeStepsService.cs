using System.Reflection;
using Microsoft.Extensions.Caching.Memory;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Recipes.Core.Services;

public sealed class RecipeStepsService
{
    private readonly IEnumerable<IRecipeStepHandler> _handlers;
    private readonly IMemoryCache _memoryCache;

    private string[] _names = null;

    public RecipeStepsService(
        IEnumerable<IRecipeStepHandler> handlers,
        IMemoryCache memoryCache)
    {
        _handlers = handlers;
        _memoryCache = memoryCache;
        _names = _memoryCache.TryGetValue("RecipeStepNames", out string[] cachedNames)
            ? cachedNames
            : null;
    }

    public IEnumerable<string> GetRecipeStepNames()
    {
        _names ??= _handlers
             .Where(h =>
                 h.GetType() == typeof(NamedRecipeStepHandler) ||
                 h.GetType().IsSubclassOf(typeof(NamedRecipeStepHandler)))
             .Select(h =>
                 (string)h.GetType()
                     .GetField("StepName", BindingFlags.Instance | BindingFlags.NonPublic)
                     ?.GetValue(h))
             .Where(name => name != null)
             .Distinct()
             .ToArray() ?? [];

        _memoryCache.Set(_names, _names, TimeSpan.FromHours(1));

        return _names;
    }
}
