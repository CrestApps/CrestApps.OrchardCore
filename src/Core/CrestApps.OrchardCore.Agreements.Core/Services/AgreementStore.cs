using CrestApps.OrchardCore.Agreements.Core.Indexes;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Agreements.Core.Services;

/// <summary>
/// Stores agreements.
/// </summary>
public sealed class AgreementStore : DocumentCatalog<Agreement, AgreementIndex>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AgreementStore"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public AgreementStore(ISession session)
        : base(session)
    {
        CollectionName = AgreementsConstants.CollectionName;
    }
}
