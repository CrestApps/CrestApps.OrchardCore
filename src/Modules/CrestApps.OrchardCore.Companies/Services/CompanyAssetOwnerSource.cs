using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Assets.Services;
using CrestApps.OrchardCore.Companies.Indexes;
using CrestApps.OrchardCore.Companies.Models;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Companies.Services;

/// <summary>
/// Lets companies own assets: finds them by name for the asset editor.
/// </summary>
internal sealed class CompanyAssetOwnerSource : IAssetOwnerSource
{
    private readonly ISession _session;

    public CompanyAssetOwnerSource(ISession session)
    {
        _session = session;
    }

    public AssetOwnerKind Kind
        => AssetOwnerKind.Company;

    public async Task<IReadOnlyList<AssetOwnerOption>> SearchAsync(string query, int count, CancellationToken cancellationToken = default)
    {
        var search = query?.Trim();

        var companies = string.IsNullOrEmpty(search)
            ? _session.Query<Company, CompanyIndex>(collection: CompaniesConstants.CollectionName)
            : _session.Query<Company, CompanyIndex>(index => index.Name.Contains(search), collection: CompaniesConstants.CollectionName);

        return [.. (await companies.OrderBy(index => index.Name).Take(count).ListAsync(cancellationToken)).Select(company => new AssetOwnerOption(company.ItemId, company.Name))];
    }

    public async Task<IReadOnlyDictionary<string, string>> GetNamesAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var companies = await _session.QueryIndex<CompanyIndex>(index => index.ItemId.IsIn(ids), collection: CompaniesConstants.CollectionName).ListAsync(cancellationToken);

        return companies.ToDictionary(index => index.ItemId, index => index.Name, StringComparer.Ordinal);
    }
}
