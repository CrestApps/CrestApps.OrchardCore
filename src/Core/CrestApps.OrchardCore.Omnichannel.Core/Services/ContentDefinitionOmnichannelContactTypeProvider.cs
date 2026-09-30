using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Options;
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
    private readonly OmnichannelCrmOptions _crmOptions;

    private IReadOnlyCollection<string> _contactContentTypes;
    private IReadOnlyCollection<string> _leadContentTypes;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentDefinitionOmnichannelContactTypeProvider"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager used to read the type definitions.</param>
    /// <param name="crmOptions">Whether the CRM feature is enabled, which decides whether lead types exist.</param>
    public ContentDefinitionOmnichannelContactTypeProvider(
        IContentDefinitionManager contentDefinitionManager,
        IOptions<OmnichannelCrmOptions> crmOptions)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _crmOptions = crmOptions.Value;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyCollection<string>> GetContactContentTypesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync();

        return _contactContentTypes;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyCollection<string>> GetLeadContentTypesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync();

        return _leadContentTypes;
    }

    private async Task EnsureLoadedAsync()
    {
        if (_contactContentTypes is not null)
        {
            return;
        }

        var definitions = await _contentDefinitionManager.ListTypeDefinitionsAsync();

        _leadContentTypes = _crmOptions.Enabled
            ? definitions
                .Where(OmnichannelRecordKinds.IsLead)
                .Select(definition => definition.Name)
                .ToArray()
            : [];

        _contactContentTypes = definitions
            .Where(OmnichannelRecordKinds.IsReachable)
            .Select(definition => definition.Name)
            .ToArray();
    }
}
