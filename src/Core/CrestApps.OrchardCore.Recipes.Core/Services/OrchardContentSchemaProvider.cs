using CrestApps.OrchardCore.Recipes.Core.Schemas;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Recipes.Core.Services;

/// <summary>
/// Default implementation that resolves part/field names from
/// Orchard Core's content management infrastructure.
/// </summary>
public sealed class OrchardContentSchemaProvider : IContentSchemaProvider
{
    private readonly IContentDefinitionManager _defManager;
    private readonly IEnumerable<Type> _partTypes;
    private readonly IEnumerable<Type> _fieldTypes;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchardContentSchemaProvider"/> class.
    /// </summary>
    /// <param name="defManager">The content definition manager for retrieving type and part definitions.</param>
    /// <param name="parts">The registered content parts.</param>
    /// <param name="fields">The registered content fields.</param>
    /// <param name="options">The content options containing part and field type registrations.</param>
    public OrchardContentSchemaProvider(
        IContentDefinitionManager defManager,
        IEnumerable<ContentPart> parts,
        IEnumerable<ContentField> fields,
        IOptions<ContentOptions> options)
    {
        _defManager = defManager;

        _partTypes = parts.Select(p => p.GetType())
            .Union(options.Value.ContentPartOptions.Select(o => o.Type));

        _fieldTypes = fields.Select(f => f.GetType())
            .Union(options.Value.ContentFieldOptions.Select(o => o.Type));
    }

    /// <summary>
    /// Gets the names of all available content parts, excluding parts that share a name with a content type.
    /// </summary>
    public async Task<IEnumerable<string>> GetPartNamesAsync()
    {
        var typeNames = new HashSet<string>(
            (await _defManager.ListTypeDefinitionsAsync()).Select(t => t.Name));

        var definedPartNames = (await _defManager.ListPartDefinitionsAsync())
            .Where(pd => !typeNames.Contains(pd.Name))
            .Select(pd => pd.Name);

        var codePartNames = _partTypes
            .Select(t => t.Name)
            .Where(n => !typeNames.Contains(n));

        return codePartNames.Union(definedPartNames).Distinct().OrderBy(n => n);
    }

    /// <summary>
    /// Gets the type names of all available content fields.
    /// </summary>
    public Task<IEnumerable<string>> GetFieldTypeNamesAsync()
        => Task.FromResult(_fieldTypes.Select(t => t.Name));
}
