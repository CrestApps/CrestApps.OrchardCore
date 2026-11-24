using System.Security.Claims;
using CrestApps.OrchardCore.ContentTransfer.Models;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.ContentTransfer.Services;

/// <summary>
/// The default <see cref="IContentTransferImportTargetResolver"/>: a registered <see cref="IContentTransferRecordImporter"/>
/// by name, or else a content type that allows bulk import. An importer's name must hold a dot, which no content type
/// name can, so an importer never shadows a content type.
/// </summary>
public sealed class DefaultContentTransferImportTargetResolver : IContentTransferImportTargetResolver
{
    private readonly IEnumerable<IContentTransferRecordImporter> _importers;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IContentManager _contentManager;
    private readonly IContentImportManager _contentImportManager;
    private readonly IAuthorizationService _authorizationService;

    public DefaultContentTransferImportTargetResolver(
        IEnumerable<IContentTransferRecordImporter> importers,
        IContentDefinitionManager contentDefinitionManager,
        IContentManager contentManager,
        IContentImportManager contentImportManager,
        IAuthorizationService authorizationService)
    {
        _importers = importers;
        _contentDefinitionManager = contentDefinitionManager;
        _contentManager = contentManager;
        _contentImportManager = contentImportManager;
        _authorizationService = authorizationService;
    }

    /// <inheritdoc/>
    public async Task<IContentTransferImportTarget> FindAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (FindImporter(name) is { } importer)
        {
            return importer;
        }

        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(name);

        return definition is not null && definition.GetSettings<ContentTypeTransferSettings>().AllowBulkImport
            ? new ContentTypeImportTarget(definition, _contentManager, _contentImportManager)
            : null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IContentTransferRecordImporter>> ListRecordImportersAsync(ClaimsPrincipal user)
    {
        var allowed = new List<IContentTransferRecordImporter>();

        foreach (var importer in _importers.Where(IsRecordName))
        {
            if (await importer.AuthorizeAsync(user, _authorizationService))
            {
                allowed.Add(importer);
            }
        }

        return allowed;
    }

    /// <inheritdoc/>
    public async Task<bool> CanImportAsync(ClaimsPrincipal user, string name)
        => user is not null
        && await FindAsync(name) is { } target
        && await target.AuthorizeAsync(user, _authorizationService);

    private IContentTransferRecordImporter FindImporter(string name)
        => _importers.FirstOrDefault(importer => IsRecordName(importer) && string.Equals(importer.Name, name, StringComparison.OrdinalIgnoreCase));

    private static bool IsRecordName(IContentTransferRecordImporter importer)
        => importer?.Name?.Contains('.', StringComparison.Ordinal) == true;
}

/// <summary>
/// A content type as an import target. Its rows are imported by the content item pipeline: the import handlers of its
/// parts and fields, the row filters and the file reshapers.
/// </summary>
public sealed class ContentTypeImportTarget : IContentTransferImportTarget
{
    private readonly IContentManager _contentManager;
    private readonly IContentImportManager _contentImportManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentTypeImportTarget"/> class.
    /// </summary>
    /// <param name="definition">The content type.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="contentImportManager">The content import manager.</param>
    public ContentTypeImportTarget(ContentTypeDefinition definition, IContentManager contentManager, IContentImportManager contentImportManager)
    {
        ArgumentNullException.ThrowIfNull(definition);

        Definition = definition;
        _contentManager = contentManager;
        _contentImportManager = contentImportManager;
    }

    /// <summary>
    /// Gets the content type.
    /// </summary>
    public ContentTypeDefinition Definition { get; }

    /// <inheritdoc/>
    public string Name => Definition.Name;

    /// <inheritdoc/>
    public string DisplayName => Definition.DisplayName;

    /// <inheritdoc/>
    public bool IsContentType => true;

    /// <inheritdoc/>
    public Task<bool> AuthorizeAsync(ClaimsPrincipal user, IAuthorizationService authorizationService)
    {
        ArgumentNullException.ThrowIfNull(authorizationService);

        // The import permission of this content type, as the content import always checked it.
        return authorizationService.AuthorizeAsync(user, ContentTransferPermissions.ImportContentFromFile, (object)Definition.Name);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ImportColumn>> GetColumnsAsync(CancellationToken cancellationToken = default)
        => await _contentImportManager.GetColumnsAsync(new ImportContentContext
        {
            ContentItem = await _contentManager.NewAsync(Definition.Name),
            ContentTypeDefinition = Definition,
        });
}
