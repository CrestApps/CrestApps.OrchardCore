using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Companies.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Companies.Indexes;

public sealed class CompanyIndex : CatalogItemIndex
{
    public string Name { get; set; }

    public bool IsEnabled { get; set; }
}

/// <summary>
/// One row for each of a company's buyers, so a signed-in user's company is found by their user id.
/// </summary>
public sealed class CompanyBuyerIndex : MapIndex
{
    public string CompanyId { get; set; }

    public string UserId { get; set; }
}

public sealed class CompanyIndexProvider : IndexProvider<Company>
{
    public CompanyIndexProvider()
    {
        CollectionName = CompaniesConstants.CollectionName;
    }

    public override void Describe(DescribeContext<Company> context)
    {
        context.For<CompanyIndex>()
            .Map(company => new CompanyIndex
            {
                ItemId = company.ItemId,
                Name = company.Name,
                IsEnabled = company.IsEnabled,
            });

        context.For<CompanyBuyerIndex>()
            .Map(company => company.Buyers
                .Where(buyer => !string.IsNullOrEmpty(buyer.UserId))
                .Select(buyer => new CompanyBuyerIndex
                {
                    CompanyId = company.ItemId,
                    UserId = buyer.UserId,
                }));
    }
}
