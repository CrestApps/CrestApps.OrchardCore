using CrestApps.Core.Services;
using CrestApps.OrchardCore.Companies.Indexes;
using CrestApps.OrchardCore.Companies.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Companies.Services;

public interface ICompanyStore : ICatalog<Company>
{
    Task<(int Count, IReadOnlyList<Company> Companies)> PageAsync(string search, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the company a user buys for, or <see langword="null"/>.
    /// </summary>
    Task<Company> FindByBuyerAsync(string userId, CancellationToken cancellationToken = default);
}

public sealed class CompanyStore : DocumentCatalog<Company, CompanyIndex>, ICompanyStore
{
    public CompanyStore(ISession session)
        : base(session)
    {
        CollectionName = CompaniesConstants.CollectionName;
    }

    public Task<(int Count, IReadOnlyList<Company> Companies)> PageAsync(string search, int skip, int take, CancellationToken cancellationToken = default)
        => PageByNameAsync(index => index.Name, search, skip, take, cancellationToken);

    public async Task<Company> FindByBuyerAsync(string userId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(userId)
            ? null
            : await Session.Query<Company, CompanyBuyerIndex>(index => index.UserId == userId, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);
}
