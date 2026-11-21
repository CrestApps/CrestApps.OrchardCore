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
