using System.Security.Claims;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Contents.ViewModels;
using YesSql;
using YesSql.Filters.Query;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

public sealed class LeadListCountQueryFactoryTests
{
    [Fact]
    public async Task TryCreateAsync_CountsTheLeadsTheListShows()
    {
        var databasePath = LeadTestStore.DatabasePath("list-count");
        var store = await LeadTestStore.CreateAsync($"Data Source={databasePath};Pooling=False");

        try
        {
            // Arrange
            await using (var seedSession = store.CreateSession())
            {
                await SaveLeadAsync(seedSession, converted: false, published: true, latest: true);
                await SaveLeadAsync(seedSession, converted: false, published: true, latest: true);
                await SaveLeadAsync(seedSession, converted: true, published: true, latest: true);

                // An older version of a lead is not on the list.
                await SaveLeadAsync(seedSession, converted: false, published: true, latest: false);

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var session = store.CreateSession();
            var factory = await CreateFactoryAsync(session, CreateHttpContext("Lead"), authorized: true);

            // Act
            var countQuery = await factory.TryCreateAsync(CreateModel("converted:false"));

            // Assert
            Assert.NotNull(countQuery);

            var listCount = await session
                .Query<ContentItem>()
                .With<ContentItemIndex>(index => index.ContentType.IsIn(new[] { "Lead" }) && index.Latest)
                .With<LeadIndex>(index => index.IsConverted == false)
                .CountAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, listCount);
            Assert.Equal(listCount, await countQuery.CountAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Theory]
    [InlineData("Contact", "", true)]
    [InlineData("Lead,Contact", "", true)]
    [InlineData("Lead", "Options.SelectedCulture=en", true)]
    [InlineData("Lead", "", false)]
    public async Task TryCreateAsync_WhenTheLeadIndexCannotAnswerTheList_LeavesTheListItsOwnCount(
        string contentTypeId,
        string queryString,
        bool authorized)
    {
        // Arrange
        var httpContext = CreateHttpContext(contentTypeId);
        httpContext.Request.QueryString = string.IsNullOrEmpty(queryString)
            ? QueryString.Empty
            : new QueryString($"?{queryString}");

        var factory = await CreateFactoryAsync(Mock.Of<ISession>(), httpContext, authorized);

        // Act
        var countQuery = await factory.TryCreateAsync(CreateModel("converted:false"));

        // Assert
        Assert.Null(countQuery);
    }

    [Fact]
    public async Task TryCreateAsync_WhenNoLeadTermIsUsed_LeavesTheListItsOwnCount()
    {
        // Arrange
        // A lead saved without its part has no lead index row, so only a lead term keeps it off the list.
        var factory = await CreateFactoryAsync(Mock.Of<ISession>(), CreateHttpContext("Lead"), authorized: true);

        // Act
        var countQuery = await factory.TryCreateAsync(CreateModel(string.Empty));

        // Assert
        Assert.Null(countQuery);
    }

    private static async Task SaveLeadAsync(ISession session, bool converted, bool published, bool latest)
    {
        var lead = new ContentItem
        {
            ContentItemId = IdGenerator.GenerateId(),
            ContentItemVersionId = IdGenerator.GenerateId(),
            ContentType = "Lead",
            DisplayText = "Lead",
            Published = published,
            Latest = latest,
        };

        lead.Alter<LeadPart>(part => part.IsConverted = converted);

        await session.SaveAsync(lead, cancellationToken: TestContext.Current.CancellationToken);
    }

    private static ContentOptionsViewModel CreateModel(string filter)
    {
        var builder = new QueryEngineBuilder<ContentItem>();
        new CrmContentsAdminListFilterProvider().Build(builder);

        return new ContentOptionsViewModel
        {
            FilterResult = builder.Build().Parse(filter),
        };
    }

    private static DefaultHttpContext CreateHttpContext(string contentTypeId)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-id")], "Test")),
        };

        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.RouteValues["contentTypeId"] = contentTypeId;

        return httpContext;
    }

    private static async Task<LeadListCountQueryFactory> CreateFactoryAsync(ISession session, HttpContext httpContext, bool authorized)
    {
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object>(),
                It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(authorized ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        var definitions = new[]
        {
            new ContentTypeDefinitionBuilder().WithName("Contact").WithPart("OmnichannelContactPart").Build(),
            new ContentTypeDefinitionBuilder().WithName("Lead").WithPart("OmnichannelContactPart").WithPart("LeadPart").Build(),
        };

        var contentDefinitionManager = new Mock<IContentDefinitionManager>();
        contentDefinitionManager
            .Setup(manager => manager.ListTypeDefinitionsAsync())
            .ReturnsAsync(definitions);

        var contentTypeProvider = new OmnichannelContentTypeProvider(
            Mock.Of<IServiceScopeFactory>(),
            Options.Create(new OmnichannelCrmOptions { Enabled = true }));
        await contentTypeProvider.EnsureInitializedAsync(contentDefinitionManager.Object);

        return new LeadListCountQueryFactory(
            session,
            new HttpContextAccessor { HttpContext = httpContext },
            authorizationService.Object,
            contentTypeProvider,
            Mock.Of<IServiceProvider>());
    }
}
