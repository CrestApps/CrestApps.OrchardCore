namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Abstraction that supplies the list of known content parts and field types
/// to the content-definition recipe step schema builder.
/// </summary>
public interface IContentSchemaProvider
{
    /// <summary>Returns names of all known content parts (code-defined + user-defined).</summary>
    Task<IEnumerable<string>> GetPartNamesAsync();

    /// <summary>Returns CLR type names of all registered content fields.</summary>
    Task<IEnumerable<string>> GetFieldTypeNamesAsync();
}
