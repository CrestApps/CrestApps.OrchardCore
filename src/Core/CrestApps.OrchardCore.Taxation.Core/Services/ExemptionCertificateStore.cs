using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Document-backed <see cref="IExemptionCertificateStore"/> implementation.
/// </summary>
public sealed class ExemptionCertificateStore : Catalog<ExemptionCertificate>, IExemptionCertificateStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExemptionCertificateStore"/> class.
    /// </summary>
    /// <param name="documentManager">The document manager for the backing document.</param>
    public ExemptionCertificateStore(IDocumentManager<DictionaryDocument<ExemptionCertificate>> documentManager)
        : base(documentManager)
    {
    }
}
