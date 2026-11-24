namespace CrestApps.OrchardCore.Learning.Credentials.Services;

/// <summary>
/// Reads the published credential definitions.
/// </summary>
public interface ICredentialDefinitionCatalog
{
    /// <summary>
    /// Finds a published definition.
    /// </summary>
    /// <param name="definitionId">The content item id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The definition, or <see langword="null"/> when it is not published.</returns>
    Task<CredentialDefinition> FindAsync(string definitionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the published definitions.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The definitions.</returns>
    Task<IReadOnlyList<CredentialDefinition>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the published definitions that have a rule of a kind on a target.
    /// </summary>
    /// <param name="kind">The rule kind.</param>
    /// <param name="targetId">The target id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The definitions.</returns>
    Task<IReadOnlyList<CredentialDefinition>> ListByRuleAsync(string kind, string targetId, CancellationToken cancellationToken = default);
}
