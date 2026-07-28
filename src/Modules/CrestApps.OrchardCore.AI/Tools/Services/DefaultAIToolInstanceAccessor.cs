using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Services;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.AI.Tools.Services;

/// <summary>
/// Default implementation of <see cref="IAIToolInstanceAccessor"/> that filters the stored tool instances
/// using the current user's tool permissions.
/// </summary>
/// <remarks>
/// The tool instances catalog is only registered when the tool instances feature is enabled, so it is
/// resolved as a collection to keep this service usable while the feature is disabled.
/// </remarks>
public sealed class DefaultAIToolInstanceAccessor : IAIToolInstanceAccessor
{
    private readonly IEnumerable<ISourceCatalog<AIToolInstance>> _instanceCatalogs;
    private readonly IAIToolAccessEvaluator _toolAccessEvaluator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAIToolInstanceAccessor"/> class.
    /// </summary>
    /// <param name="instanceCatalogs">The tool instances catalogs, which is empty when the feature is disabled.</param>
    /// <param name="toolAccessEvaluator">The evaluator used to filter out inaccessible instances.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor used to resolve the current user.</param>
    public DefaultAIToolInstanceAccessor(
        IEnumerable<ISourceCatalog<AIToolInstance>> instanceCatalogs,
        IAIToolAccessEvaluator toolAccessEvaluator,
        IHttpContextAccessor httpContextAccessor)
    {
        _instanceCatalogs = instanceCatalogs;
        _toolAccessEvaluator = toolAccessEvaluator;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public async Task<IList<AIToolInstance>> GetAccessibleInstancesAsync(CancellationToken cancellationToken = default)
    {
        var accessible = new List<AIToolInstance>();
        // The last registration wins when the container resolves a single catalog, so the same one is used here.
        var catalog = _instanceCatalogs.LastOrDefault();
        var user = _httpContextAccessor.HttpContext?.User;

        if (catalog is null || user is null)
        {
            return accessible;
        }

        foreach (var instance in await catalog.GetAllAsync(cancellationToken))
        {
            if (await _toolAccessEvaluator.IsAuthorizedAsync(user, instance.GetFunctionName()))
            {
                accessible.Add(instance);
            }
        }

        return accessible;
    }
}
