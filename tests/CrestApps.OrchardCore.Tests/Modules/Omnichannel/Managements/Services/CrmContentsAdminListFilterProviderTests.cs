using System.Linq.Expressions;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Contents.Services;
using YesSql;
using YesSql.Filters.Query;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

public sealed class CrmContentsAdminListFilterProviderTests
{
    [Fact]
    public async Task Build_WhenRegisteredAfterTheContentListStatusTerm_KeepsTheStatusTermRunning()
    {
        // Arrange
        // Orchard Core's 'status' term always runs and limits the content list to the latest version of each item.
        // The CRM used to register its own 'status' term, which replaced Orchard Core's, so every content list showed
        // every archived version. This stands in for Orchard Core's term, registered first as it is in a tenant.
        var statusTermRan = false;
        var builder = new QueryEngineBuilder<ContentItem>();
        builder.WithNamedTerm("status", term => term
            .OneCondition((value, query) =>
            {
                statusTermRan = true;

                return query;
            })
            .AlwaysRun());

        new CrmContentsAdminListFilterProvider().Build(builder);

        // Act
        await builder.Build().Parse(string.Empty).ExecuteAsync(new Mock<IQuery<ContentItem>>().Object);

        // Assert
        Assert.True(statusTermRan);
    }

    [Fact]
    public async Task Build_WhenLeadStatusTermIsParsed_FiltersLeadsByTheStatusId()
    {
        // Arrange
        var builder = new QueryEngineBuilder<ContentItem>();
        new CrmContentsAdminListFilterProvider().Build(builder);

        var catalog = new Mock<INamedCatalog<LeadStatus>>();
        catalog
            .Setup(x => x.FindByNameAsync("Nurturing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LeadStatus { ItemId = "nurturing-id" });

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(x => x.GetService(typeof(INamedCatalog<LeadStatus>)))
            .Returns(catalog.Object);

        Expression<Func<LeadIndex, bool>> predicate = null;
        var query = new Mock<IQuery<ContentItem>>();
        query
            .Setup(x => x.With<LeadIndex>(It.IsAny<Expression<Func<LeadIndex, bool>>>()))
            .Callback<Expression<Func<LeadIndex, bool>>>(value => predicate = value)
            .Returns(new Mock<IQuery<ContentItem, LeadIndex>>().Object);

        // Act
        await builder.Build()
            .Parse($"{CrmContentsAdminListFilterProvider.LeadStatusTermName}:Nurturing")
            .ExecuteAsync(new ContentQueryContext(serviceProvider.Object, query.Object));

        // Assert
        Assert.NotNull(predicate);
        Assert.True(predicate.Compile()(new LeadIndex { StatusId = "nurturing-id" }));
        Assert.False(predicate.Compile()(new LeadIndex { StatusId = "other-id" }));
    }
}
