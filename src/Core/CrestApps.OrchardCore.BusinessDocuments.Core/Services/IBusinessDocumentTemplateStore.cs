using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessDocuments.Models;

namespace CrestApps.OrchardCore.BusinessDocuments.Core.Services;

/// <summary>
/// Stores document templates.
/// </summary>
public interface IBusinessDocumentTemplateStore : ICatalog<BusinessDocumentTemplate>
{
    /// <summary>
    /// Returns the templates marked as the default for their kinds.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The default templates.</returns>
    Task<IReadOnlyList<BusinessDocumentTemplate>> GetDefaultsAsync(CancellationToken cancellationToken = default);
}
