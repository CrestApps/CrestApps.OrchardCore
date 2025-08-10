using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentManagement.Utilities;

namespace CrestApps.OrchardCore.AI.Agent.Services;

public sealed class ContentMetadataService
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    private readonly IEnumerable<Type> _contentPartTypes;
    private readonly IEnumerable<Type> _contentFieldTypes;

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

    public Task<IEnumerable<Type>> GetFieldsAsync()
        => Task.FromResult(_contentFieldTypes);
}

public sealed class ContentPartMetadata
{
    public ContentPartMetadata()
    {
    }

    public ContentPartMetadata(ContentPartDefinition contentPartDefinition)
    {
        Name = contentPartDefinition.Name;
        PartDefinition = contentPartDefinition;
        _displayName = contentPartDefinition.DisplayName();
    }

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

    public ContentPartDefinition PartDefinition { get; private set; }
}
