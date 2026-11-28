using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using System.Security.Claims;

namespace CrestApps.OrchardCore.Tests.Surveys.Authoring;

public sealed class SurveyAuthorizationServiceTests
{
    private static readonly Survey _ownSurvey = new() { ItemId = "s1", OwnerId = "pat-id" };

    [Fact]
    public async Task AuthorizeAsync_GlobalPermission_Allows()
    {
        var service = new SurveyAuthorizationService(TestSecurity.Granting(SurveysPermissions.ManageSurveys), []);

        Assert.True(await service.AuthorizeAsync(TestSecurity.User("lee"), _ownSurvey, SurveyOperation.Manage, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AuthorizeAsync_OwnPermissionOnOwnSurvey_Allows()
    {
        var service = new SurveyAuthorizationService(TestSecurity.Granting(SurveysPermissions.ManageOwnSurveys), []);

        Assert.True(await service.AuthorizeAsync(TestSecurity.User("pat"), _ownSurvey, SurveyOperation.Manage, TestContext.Current.CancellationToken));
        Assert.False(await service.AuthorizeAsync(TestSecurity.User("lee"), _ownSurvey, SurveyOperation.Manage, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AuthorizeAsync_OwnerWhoseTokenCarriesOnlyASubClaim_IsTheOwner()
    {
        // Arrange: R12-L13, an API token that names the user with "sub" only, as the API's caller id already reads it.
        var service = new SurveyAuthorizationService(TestSecurity.Granting(SurveysPermissions.ManageOwnSurveys), []);
        var owner = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "pat-id")], "Bearer"));
        var other = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "lee-id")], "Bearer"));

        // Act
        var ownerAllowed = await service.AuthorizeAsync(owner, _ownSurvey, SurveyOperation.Manage, TestContext.Current.CancellationToken);
        var otherAllowed = await service.AuthorizeAsync(other, _ownSurvey, SurveyOperation.Manage, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(ownerAllowed);
        Assert.False(otherAllowed);
    }

    [Theory]
    [InlineData(SurveyOperation.ManageInvitations)]
    [InlineData(SurveyOperation.Publish)]
    [InlineData(SurveyOperation.DeleteResponses)]
    [InlineData(SurveyOperation.Grade)]
    [InlineData(SurveyOperation.ManageOverrides)]
    public async Task AuthorizeAsync_ManageOwnSurveysAlone_DoesNotGrantTheDedicatedOperations(SurveyOperation operation)
    {
        // Arrange: the Editor role's survey permissions, without the dedicated own permissions.
        var service = new SurveyAuthorizationService(TestSecurity.Granting(SurveysPermissions.ManageOwnSurveys, SurveysPermissions.ViewOwnSurveyResponses), []);

        // Act
        var allowed = await service.AuthorizeAsync(TestSecurity.User("pat"), _ownSurvey, operation, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(allowed);
    }

    [Fact]
    public async Task AuthorizeAsync_ManageOwnSurveysAlone_CannotSendInvitations()
    {
        // Arrange
        var service = new SurveyAuthorizationService(TestSecurity.Granting(SurveysPermissions.ManageOwnSurveys), []);

        // Act
        var canManage = await service.AuthorizeAsync(TestSecurity.User("pat"), _ownSurvey, SurveyOperation.Manage, TestContext.Current.CancellationToken);
        var canInvite = await service.AuthorizeAsync(TestSecurity.User("pat"), _ownSurvey, SurveyOperation.ManageInvitations, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(canManage);
        Assert.False(canInvite);
    }

    [Fact]
    public async Task AuthorizeAsync_OwnInvitationsPermission_AllowsOnlyOwnSurveys()
    {
        // Arrange
        var service = new SurveyAuthorizationService(TestSecurity.Granting(SurveysPermissions.ManageOwnSurveyInvitations), []);

        // Act
        var own = await service.AuthorizeAsync(TestSecurity.User("pat"), _ownSurvey, SurveyOperation.ManageInvitations, TestContext.Current.CancellationToken);
        var other = await service.AuthorizeAsync(TestSecurity.User("lee"), _ownSurvey, SurveyOperation.ManageInvitations, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(own);
        Assert.False(other);
    }

    [Theory]
    [InlineData(SurveyOperation.Publish)]
    [InlineData(SurveyOperation.DeleteResponses)]
    [InlineData(SurveyOperation.Grade)]
    [InlineData(SurveyOperation.ManageOverrides)]
    [InlineData(SurveyOperation.ManageInvitations)]
    public void PermissionsOf_EveryOwnPermission_IsImpliedByItsGlobalPermission(SurveyOperation operation)
    {
        // Act
        var (global, own) = SurveyAuthorizationService.PermissionsOf(operation);

        // Assert
        Assert.NotEqual(SurveysPermissions.ManageOwnSurveys, own);
        Assert.Contains(global, own.ImpliedBy);
    }

    [Fact]
    public async Task AuthorizeAsync_ExportNeedsTheGlobalPermission()
    {
        var service = new SurveyAuthorizationService(TestSecurity.Granting(SurveysPermissions.ManageOwnSurveys, SurveysPermissions.ViewOwnSurveyResponses), []);

        Assert.False(await service.AuthorizeAsync(TestSecurity.User("pat"), _ownSurvey, SurveyOperation.ExportResponses, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AuthorizeAsync_AccessProvider_CanGrant()
    {
        var service = new SurveyAuthorizationService(TestSecurity.Granting(), [new GrantingProvider()]);

        Assert.True(await service.AuthorizeAsync(TestSecurity.User("lee"), _ownSurvey, SurveyOperation.Grade, TestContext.Current.CancellationToken));
        Assert.False(await service.AuthorizeAsync(TestSecurity.User("lee"), _ownSurvey, SurveyOperation.Manage, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AuthorizeAsync_Anonymous_IsRefused()
    {
        var service = new SurveyAuthorizationService(TestSecurity.Granting(SurveysPermissions.ManageSurveys), []);

        Assert.False(await service.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), _ownSurvey, SurveyOperation.Manage, TestContext.Current.CancellationToken));
    }

    private sealed class GrantingProvider : ISurveyAccessProvider
    {
        public Task<bool> GrantsAsync(ClaimsPrincipal user, Survey survey, SurveyOperation operation, CancellationToken cancellationToken = default)
            => Task.FromResult(operation == SurveyOperation.Grade);
    }
}
