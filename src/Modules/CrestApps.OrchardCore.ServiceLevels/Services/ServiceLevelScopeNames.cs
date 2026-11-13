using Microsoft.Extensions.Localization;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.ServiceLevels.Services;

/// <summary>
/// Gives the name a policy's scope is shown by.
/// </summary>
internal static class ServiceLevelScopeNames
{
    /// <summary>
    /// Gets the localized display name of a scope, or the stored name when no enabled feature registers the scope.
    /// </summary>
    /// <param name="localizerFactory">The localizer factory.</param>
    /// <param name="scopes">The registered scopes.</param>
    /// <param name="scope">The scope's stored name.</param>
    /// <returns>The name to show.</returns>
    public static string Get(IStringLocalizerFactory localizerFactory, IEnumerable<IServiceLevelScopeProvider> scopes, string scope)
    {
        var provider = scopes.FirstOrDefault(item => string.Equals(item.Name, scope, StringComparison.OrdinalIgnoreCase));

        return provider?.DisplayName is { } displayName
            ? localizerFactory.Localize(displayName).Value
            : scope;
    }
}
