using System.Net;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Learning.Commerce.Models;
using CrestApps.OrchardCore.Learning.Commerce.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Learning.IntegrationTests;

/// <summary>
/// Learning commerce on a running site: courses become products, the order grants handler is registered, and the
/// course seats page shows an account admin the account's pools and is refused to guests and to people who are not
/// an admin of an account.
/// </summary>
[Collection(LearningSiteCollection.Name)]
public sealed class LearningCommercePagesTests
{
    private readonly LearningSiteFixture _fixture;

    public LearningCommercePagesTests(LearningSiteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Courses_AreProducts_AndPaidOrdersReachTheLearningGrants()
    {
        // Act
        var (parts, handlers) = await _fixture.InTenantAsync(async services =>
        {
            var definition = await services.GetRequiredService<IContentDefinitionManager>().GetTypeDefinitionAsync(LearningConstants.ContentTypes.Course);
            var handlerNames = services.GetServices<IOrderEventHandler>().Select(handler => handler.GetType().Name).ToArray();

            return (definition.Parts.Select(part => part.PartDefinition.Name).ToArray(), handlerNames);
        });

        // Assert
        Assert.Contains("ProductPart", parts);
        Assert.Contains(nameof(LearningProductPart), parts);
        Assert.Contains("LearningOrderEventHandler", handlers);
    }

    [Fact]
    public async Task TheSeatsPage_ShowsAnAccountAdminTheirPools_AndIsRefusedToOthers()
    {
        // Arrange: learner2 is the admin of an account that bought seats in the fixture course.
        var poolTitle = "Seats " + Guid.NewGuid().ToString("n")[..6];

        await _fixture.InTenantAsync(async services =>
        {
            var user = await services.GetRequiredService<UserManager<IUser>>().FindByNameAsync("learner2");
            var userId = await services.GetRequiredService<UserManager<IUser>>().GetUserIdAsync(user);
            var contentManager = services.GetRequiredService<IContentManager>();
            var account = await contentManager.NewAsync("Account");

            account.DisplayText = "Acme " + poolTitle;
            account.Apply(new AccountMembersPart
            {
                Members = [new AccountMember { UserId = userId, UserName = "learner2", Email = "learner2@example.com", Role = AccountMemberRole.Admin }],
            });

            await contentManager.CreateAsync(account, VersionOptions.Published);

            await services.GetRequiredService<ILicensePoolService>().CreateAsync(new LicensePool
            {
                AccountId = account.ContentItemId,
                CourseId = _fixture.CourseId,
                Title = poolTitle,
                Seats = 50,
                OrderId = "order-" + poolTitle,
                LineId = "line-1",
            });
        });

        // Act
        using var admin = await _fixture.CreateClientAsync("learner2");
        var page = await admin.GetStringAsync("account/learning-seats", TestContext.Current.CancellationToken);

        using var notAnAdmin = await _fixture.CreateClientAsync("learner1");
        var refused = await notAnAdmin.GetAsync("account/learning-seats", TestContext.Current.CancellationToken);

        var anonymous = await _fixture.CreateClientAsync();
        var guest = await anonymous.GetAsync("account/learning-seats", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(poolTitle, page);
        Assert.Contains("50 of 50 seats left", page);
        Assert.NotEqual(HttpStatusCode.OK, refused.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, guest.StatusCode);
        Assert.Contains("Login", guest.Headers.Location.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
