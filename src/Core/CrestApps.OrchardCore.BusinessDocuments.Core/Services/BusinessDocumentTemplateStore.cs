using CrestApps.OrchardCore.BusinessDocuments.Core.Indexes;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.BusinessDocuments.Core.Services;

/// <summary>
/// Stores document templates in YesSql.
/// </summary>
public sealed class BusinessDocumentTemplateStore : DocumentCatalog<BusinessDocumentTemplate, BusinessDocumentTemplateIndex>, IBusinessDocumentTemplateStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessDocumentTemplateStore"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public BusinessDocumentTemplateStore(ISession session)
        : base(session)
    {
        CollectionName = BusinessDocumentsStorage.CollectionName;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BusinessDocumentTemplate>> GetDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var templates = await Session.Query<BusinessDocumentTemplate, BusinessDocumentTemplateIndex>(index => index.IsDefault, collection: BusinessDocumentsStorage.CollectionName)
            .ListAsync(cancellationToken);

        return await LoadedAsync(templates);
    }
}
