using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// The default <see cref="IOmnichannelContactTypeProvider"/>: reads the tenant's content type definitions and
/// keeps the answer for the lifetime of the scope, so a request that asks more than once pays for one read. It
/// needs nothing beyond the content definitions, which every tenant has.
/// </summary>
public sealed class ContentDefinitionOmnichannelContactTypeProvider : IOmnichannelContactTypeProvider
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    private IReadOnlyCollection<string> _contactContentTypes;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentDefinitionOmnichannelContactTypeProvider"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager used to read the type definitions.</param>
    public ContentDefinitionOmnichannelContactTypeProvider(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyCollection<string>> GetContactContentTypesAsync(CancellationToken cancellationToken = default)
    {
        if (_contactContentTypes is not null)
        {
            return _contactContentTypes;
        }

        var definitions = await _contentDefinitionManager.ListTypeDefinitionsAsync();

        _contactContentTypes = definitions
            .Where(definition => definition.Parts.Any(part => part.Name == OmnichannelConstants.ContentParts.OmnichannelContact))
            .Select(definition => definition.Name)
            .ToArray();

        return _contactContentTypes;
    }
}
