namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Abstraction that supplies the list of known content parts and field types
/// to the content-definition recipe step schema builder.
/// </summary>
public interface IContentSchemaProvider
{
    /// <summary>Returns names of all known content parts (code-defined + user-defined).</summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IEnumerable<string>> GetPartNamesAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns CLR type names of all registered content fields.</summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IEnumerable<string>> GetFieldTypeNamesAsync(CancellationToken cancellationToken = default);
}
