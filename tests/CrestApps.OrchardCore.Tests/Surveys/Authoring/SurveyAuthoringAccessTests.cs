using System.Security.Claims;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.AspNetCore.Authorization;
using Moq;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Surveys.Authoring;

public sealed class SurveyAuthoringAccessTests : IAsyncLifetime
{
    private AuthoringSurveyHarness _harness;

    public async ValueTask InitializeAsync()
        => _harness = await AuthoringSurveyHarness.CreateAsync();

    public async ValueTask DisposeAsync()
        => await _harness.DisposeAsync();

    [Theory]
    [InlineData(SurveyCollectorKinds.Kiosk, false)]
    [InlineData(SurveyCollectorKinds.Invitation, false)]
    [InlineData(SurveyCollectorKinds.WebLink, true)]
    [InlineData(SurveyCollectorKinds.Embed, true)]
    public void CanEmbed_RefusesKioskAndInvitationCollectors(string kind, bool allowed)
        => Assert.Equal(allowed, SurveyEmbedResolver.CanEmbed(new SurveyCollector { Kind = kind }));

    [Fact]
    public async Task OwnSurveysGradingScope_ListsOnlyTheSurveysTheGraderOwns()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var session = _harness.Store.CreateSession();
        var store = new SurveyStore(session);
        await store.CreateAsync(new Survey { ItemId = "mine", Name = "mine", Title = "Mine", OwnerId = "u-owner" }, cancellationToken);
        await store.CreateAsync(new Survey { ItemId = "theirs", Name = "theirs", Title = "Theirs", OwnerId = "u-other" }, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "u-owner")], "Test"));

        // Act
        var ownOnly = await new OwnSurveysGradingScope(Authorization(gradeAll: false, gradeOwn: true), store).GetGradableSurveyIdsAsync(user, cancellationToken);
        var none = await new OwnSurveysGradingScope(Authorization(gradeAll: false, gradeOwn: false), store).GetGradableSurveyIdsAsync(user, cancellationToken);

        // Assert
        Assert.Equal(["mine"], ownOnly);
        Assert.Empty(none);
    }

    private static IAuthorizationService Authorization(bool gradeAll, bool gradeOwn)
    {
        var service = new Mock<IAuthorizationService>();

        service.Setup(s => s.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal _, object _, IEnumerable<IAuthorizationRequirement> requirements) =>
            {
                var name = requirements.OfType<PermissionRequirement>().Single().Permission.Name;
                var granted = name == SurveysPermissions.GradeSurveyResponses.Name ? gradeAll : name == SurveysPermissions.GradeOwnSurveyResponses.Name && gradeOwn;

                return granted ? AuthorizationResult.Success() : AuthorizationResult.Failed();
            });

        return service.Object;
    }
}
