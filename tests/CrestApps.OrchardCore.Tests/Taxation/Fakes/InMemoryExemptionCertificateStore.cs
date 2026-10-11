using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Tests.Taxation.Fakes;

/// <summary>
/// In-memory <see cref="IExemptionCertificateStore"/> used by the taxation tests.
/// </summary>
public sealed class InMemoryExemptionCertificateStore : InMemoryNamedCatalog<ExemptionCertificate>, IExemptionCertificateStore
{
}
