using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Reports;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Security.Permissions;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Surveys.Results;

/// <summary>
/// R11-M2: the Survey Participation report lists the members of the survey's own audiences only, unless the person may
/// manage user audiences, as the Users module's own member list requires.
/// </summary>
public sealed class SurveyParticipationReportTests : IAsyncLifetime
{
    private static readonly DateTime _day = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    private ResultsSqliteStore _store;

    public async ValueTask InitializeAsync()
        => _store = await ResultsSqliteStore.CreateAsync();

    public async ValueTask DisposeAsync()
        => await _store.DisposeAsync();

    [Fact]
    public async Task RunAsync_AnAudienceTheSurveyDoesNotUse_ListsNoMember()
    {
        // Arrange: an Editor who may read the responses of their own survey picks the executives' audience.
        await SaveSurveyAsync();
        await using var session = _store.OpenSession();

        // Act
        var document = await Report(session, SurveysPermissions.ViewSurveyResponses).RunAsync(Context("executives"), TestContext.Current.CancellationToken);

        // Assert
        var section = Assert.Single(document.Sections);
        Assert.Empty(section.Rows);
        Assert.Contains("own audiences", section.EmptyMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_OneOfTheSurveysOwnAudiences_ListsItsMembers()
    {
        // Arrange
        await SaveSurveyAsync();
        await using var session = _store.OpenSession();

        // Act
        var document = await Report(session, SurveysPermissions.ViewSurveyResponses).RunAsync(Context("staff"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(document.Sections, section => section.Rows.Count == 2);
    }

    [Fact]
    public async Task RunAsync_AnAudienceTheSurveyNamedBeforeItOpenedToEveryone_ListsNoMember()
    {
        // Arrange: Who may respond changed from the staff audience to anyone; the list of audiences is kept.
        await SaveSurveyAsync(SurveyAudienceMode.Anyone);
        await using var session = _store.OpenSession();

        // Act
        var document = await Report(session, SurveysPermissions.ViewSurveyResponses).RunAsync(Context("staff"), TestContext.Current.CancellationToken);

        // Assert
        var section = Assert.Single(document.Sections);
        Assert.Empty(section.Rows);
    }

    [Fact]
    public async Task RunAsync_WithManageUserAudiences_ListsAnyAudience()
    {
        // Arrange
        await SaveSurveyAsync();
        await using var session = _store.OpenSession();

        // Act
        var document = await Report(session, SurveysPermissions.ViewSurveyResponses, CrestApps.OrchardCore.Users.Core.UserPermissions.ManageUserAudiences)
            .RunAsync(Context("executives"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(document.Sections, section => section.Rows.Count == 2);
    }

    [Fact]
    public async Task ListableAudiencesAsync_OffersTheSurveysOwnAudiencesOnly()
    {
        // Arrange
        await SaveSurveyAsync();
        await using var session = _store.OpenSession();
        var data = Data(session, TestSecurity.Granting(SurveysPermissions.ViewSurveyResponses), Services(session, TestSecurity.Granting(SurveysPermissions.ViewSurveyResponses)));

        // Act
        var listable = await data.ListableAudiencesAsync(TestSecurity.User("kim"), "s1", TestContext.Current.CancellationToken);
        var none = await data.ListableAudiencesAsync(TestSecurity.User("kim"), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(listable("staff"));
        Assert.False(listable("executives"));
        Assert.False(none("staff"));
    }

    private static SurveyParticipationReport Report(ISession session, params Permission[] grants)
    {
        var authorization = TestSecurity.Granting(grants);
        var services = Services(session, authorization);

        return new SurveyParticipationReport(
            Data(session, authorization, services),
            new SurveyResultsQueries(session),
            new SurveyRespondentNames(services, RecipeRoundTrip.Localizer<SurveyRespondentNames>()),
            services,
            RecipeRoundTrip.Localizer<SurveyParticipationReport>());
    }

    private static ServiceProvider Services(ISession session, Microsoft.AspNetCore.Authorization.IAuthorizationService authorization)
    {
        var audiences = new Mock<IUserAudienceService>();
        audiences
            .Setup(service => service.GetMembersAsync(It.IsAny<string>(), 0, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((2, (IReadOnlyList<string>)["u1", "u2"]));
        audiences
            .Setup(service => service.GetMembersAsync(It.IsAny<string>(), It.Is<int>(skip => skip > 0), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((2, (IReadOnlyList<string>)[]));
        var store = new SurveyStore(session);

        return new ServiceCollection()
            .AddSingleton(authorization)
            .AddSingleton(store)
            .AddSingleton(audiences.Object)
            .AddSingleton(new SurveyAnonymitySecrets(new EphemeralDataProtectionProvider(), store, NullLogger<SurveyAnonymitySecrets>.Instance))
            .BuildServiceProvider();
    }

    private static SurveyReportData Data(ISession session, Microsoft.AspNetCore.Authorization.IAuthorizationService authorization, IServiceProvider services)
        => new(
            new SurveyStore(session),
            new SurveyVersionStore(session),
            authorization,
            services,
            new FixedOffsetLocalClock(() => _day, TimeSpan.Zero),
            new SurveyRespondentNames(services, RecipeRoundTrip.Localizer<SurveyRespondentNames>()),
            RecipeRoundTrip.Localizer<SurveyReportData>(),
            new SurveyAccessScopes(authorization, [], new SurveyIndexReader(session, new SurveyStore(session))),
            new SurveyIndexReader(session, new SurveyStore(session)));

    private static ReportContext Context(string audience)
    {
        var filter = new ReportFilter { ReportName = SurveyReportNames.Participation };
        filter.Set(SurveyReportKeys.SurveyId, "s1");
        filter.Set(SurveyReportKeys.AudienceId, audience);

        return new ReportContext(filter) { User = TestSecurity.User("kim") };
    }

    private async Task SaveSurveyAsync(SurveyAudienceMode audience = SurveyAudienceMode.Audiences)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var version = ResultsSqliteStore.Version("s1-v1", "s1", 1, ResultsSqliteStore.ChoiceQuestion("q1", "color", "red", "blue"));

        await using var session = _store.OpenSession();
        await session.SaveAsync(new Survey
        {
            ItemId = "s1",
            Name = "s1",
            Title = "Engagement",
            Status = SurveyStatus.Open,
            PublishedVersionId = version.ItemId,
            PublishedVersionNumber = 1,
            Access = new SurveyAccessSettings { Anonymity = AnonymityMode.Identified, Audience = audience, AudienceIds = ["staff"] },
        }, false, SurveysConstants.CollectionName, cancellationToken);
        await session.SaveAsync(version, false, SurveysConstants.CollectionName, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);
    }
}
