using CrestApps.Core.Services;
using CrestApps.OrchardCore.Learning.Lti.Models;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Services;

/// <summary>
/// Stores external tools.
/// </summary>
public interface IExternalToolStore : ICatalog<ExternalTool>
{
    /// <summary>
    /// Returns the tool of an OpenID client id, or <see langword="null"/>.
    /// </summary>
    /// <param name="clientId">The client id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The tool.</returns>
    Task<ExternalTool> FindByClientIdAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every tool, by name.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The tools.</returns>
    Task<IReadOnlyList<ExternalTool>> ListAllAsync(CancellationToken cancellationToken = default);
}
