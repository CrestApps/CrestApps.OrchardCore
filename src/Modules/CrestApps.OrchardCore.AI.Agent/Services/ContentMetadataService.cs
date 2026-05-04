using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentManagement.Utilities;

namespace CrestApps.OrchardCore.AI.Agent.Services;

/// <summary>
/// Provides content metadata services.
/// </summary>
public sealed class ContentMetadataService
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    private readonly IEnumerable<Type> _contentPartTypes;
    private readonly IEnumerable<Type> _contentFieldTypes;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentMetadataService"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="contentParts">The content parts.</param>
    /// <param name="contentFields">The content fields.</param>
    /// <param name="contentOptions">The content options.</param>
    public ContentMetadataService(
        IContentDefinitionManager contentDefinitionManager,
        IEnumerable<ContentPart> contentParts,
        IEnumerable<ContentField> contentFields,
        IOptions<ContentOptions> contentOptions)
    {
        _contentDefinitionManager = contentDefinitionManager;

        _contentPartTypes = contentParts.Select(cp => cp.GetType())
            .Union(contentOptions.Value.ContentPartOptions.Select(cpo => cpo.Type));

        _contentFieldTypes = contentFields.Select(cf => cf.GetType())
            .Union(contentOptions.Value.ContentFieldOptions.Select(cfo => cfo.Type));
    }

    /// <summary>
    /// Retrieves the parts async.
    /// </summary>
    public async Task<IEnumerable<ContentPartMetadata>> GetPartsAsync()
    {
        var typeNames = new HashSet<string>(
            (await _contentDefinitionManager.ListTypeDefinitionsAsync())
                .Select(ctd => ctd.Name)
        );

        // User-defined parts
        var userContentParts = (await _contentDefinitionManager.ListPartDefinitionsAsync())
            .Where(cpd => !typeNames.Contains(cpd.Name))
            .Select(cpd => new ContentPartMetadata(cpd))
            .ToDictionary(k => k.Name);

        // Code-defined parts
        var codeDefinedParts = _contentPartTypes
            .Where(cpd => !userContentParts.ContainsKey(cpd.Name))
            .Select(cpi => new ContentPartMetadata
            {
                Name = cpi.Name,
                DisplayName = cpi.Name
            });

        return codeDefinedParts
            .Union(userContentParts.Values)
            .OrderBy(m => m.DisplayName);
    }

    /// <summary>
    /// Retrieves the fields async.
    /// </summary>
    public Task<IEnumerable<Type>> GetFieldsAsync()
        => Task.FromResult(_contentFieldTypes);
}

/// <summary>
/// Represents the content part metadata.
/// </summary>
public sealed class ContentPartMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContentPartMetadata"/> class.
    /// </summary>
    public ContentPartMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentPartMetadata"/> class.
    /// </summary>
    /// <param name="contentPartDefinition">The content part definition.</param>
    public ContentPartMetadata(ContentPartDefinition contentPartDefinition)
    {
        Name = contentPartDefinition.Name;
        PartDefinition = contentPartDefinition;
        _displayName = contentPartDefinition.DisplayName();
    }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    private string _displayName;

    public string DisplayName
    {
        get { return !string.IsNullOrWhiteSpace(_displayName) ? _displayName : Name.TrimEndString("Part").CamelFriendly(); }
        set { _displayName = value; }
    }

    public string Description
    {
        get { return PartDefinition.GetSettings<ContentPartSettings>().Description; }
        set { PartDefinition.GetSettings<ContentPartSettings>().Description = value; }
    }

    /// <summary>
    /// Gets or sets the part definition.
    /// </summary>
    public ContentPartDefinition PartDefinition { get; private set; }
}
