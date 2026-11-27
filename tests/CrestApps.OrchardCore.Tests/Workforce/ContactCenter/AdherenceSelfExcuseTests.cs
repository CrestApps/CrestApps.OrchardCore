using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.ContactCenter.Controllers;
using CrestApps.OrchardCore.Workforce.ContactCenter.Indexes;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Workforce.ContactCenter;

/// <summary>
/// C11 (review 11): recording or removing an adherence exception, and saving an adherence day, checked only that the
/// manager manages the worker, which is always true of the manager's own worker record. A manager could excuse their own
/// adherence. These now need the same <see cref="IWorkforceScopeService.CanDecideForWorkerAsync"/> check as timesheets,
/// punches and time off.
/// </summary>
public sealed class AdherenceSelfExcuseTests
{
    private static readonly DateOnly _monday = PlanningKit.Monday;

    [Fact]
    public async Task AManager_CantRecordAnExceptionOnTheirOwnRecord_ButAPeerManagerCan()
    {
        // Arrange
        await using var setup = await SetupAsync();

        // Act
        var own = await setup.Harness.RunAsync(services => Exceptions(services, Manager("user-maya")).CreatePost(Form(setup.Maya)));
        var ownCount = await CountExceptionsAsync(setup);
        var peer = await setup.Harness.RunAsync(services => Exceptions(services, Manager("user-ben")).CreatePost(Form(setup.Maya)));
        var peerCount = await CountExceptionsAsync(setup);

        // Assert
        Assert.IsType<ViewResult>(own);
        Assert.Equal(0, ownCount);
        Assert.IsType<RedirectToActionResult>(peer);
        Assert.Equal(1, peerCount);
    }

    [Fact]
    public async Task AManagerWithApproveOwnRecords_RecordsAnExceptionOnTheirOwnRecord()
    {
        // Arrange
        await using var setup = await SetupAsync();

        // Act
        var own = await setup.Harness.RunAsync(services => Exceptions(services, Manager("user-maya", WorkforcePermissions.ApproveOwnRecords.Name)).CreatePost(Form(setup.Maya)));

        // Assert
        Assert.IsType<RedirectToActionResult>(own);
        Assert.Equal(1, await CountExceptionsAsync(setup));
    }

    [Fact]
    public async Task AManager_CantRemoveAnExceptionOnTheirOwnRecord()
    {
        // Arrange: a peer recorded an exception on Maya's record; Maya mustn't be able to remove it either.
        await using var setup = await SetupAsync();
        await setup.Harness.RunAsync(services => Exceptions(services, Manager("user-ben")).CreatePost(Form(setup.Maya)));
        var id = await setup.Harness.RunAsync(services => services.GetRequiredService<ISession>()
            .QueryIndex<AdherenceExceptionIndex>(collection: WorkforceConstants.CollectionName).FirstOrDefaultAsync(), commit: false);

        // Act
        var result = await setup.Harness.RunAsync(services => Exceptions(services, Manager("user-maya")).Delete(id.ItemId));

        // Assert
        Assert.IsType<ForbidResult>(result);
        Assert.Equal(1, await CountExceptionsAsync(setup));
    }

    [Fact]
    public async Task AManager_CantSaveTheirOwnAdherenceDay()
    {
        // Arrange
        await using var setup = await SetupAsync();

        // Act
        var own = await setup.Harness.RunAsync(services => Adherence(services, Manager("user-maya")).SaveDay(setup.Maya.ItemId, _monday));
        var peer = await setup.Harness.RunAsync(services => Adherence(services, Manager("user-ben")).SaveDay(setup.Maya.ItemId, _monday));

        // Assert
        Assert.IsType<ForbidResult>(own);
        Assert.IsType<RedirectToActionResult>(peer);
    }

    private static ClaimsPrincipal Manager(string userId, params string[] extra)
        => CalendarTestHarness.User(userId, [WorkforcePermissions.ViewAdherence.Name, WorkforcePermissions.ManageAdherence.Name, .. extra]);

    private static AdherenceExceptionEditViewModel Form(StaffMember worker)
        => new()
        {
            WorkerId = worker.ItemId,
            Date = _monday,
            Start = new TimeOnly(9, 0),
            End = new TimeOnly(10, 0),
            Reason = AdherenceExceptionReason.Coaching,
        };

    private static Task<int> CountExceptionsAsync(Setup setup)
        => setup.Harness.RunAsync(services => services.GetRequiredService<ISession>()
            .QueryIndex<AdherenceExceptionIndex>(collection: WorkforceConstants.CollectionName).CountAsync(), commit: false);

    private static AdherenceExceptionsController Exceptions(IServiceProvider services, ClaimsPrincipal user)
        => WithContext(new AdherenceExceptionsController(
            services.GetRequiredService<IAuthorizationService>(),
            services.GetRequiredService<AdherenceService>(),
            services.GetRequiredService<IWorkforceScopeService>(),
            services.GetRequiredService<ITeamManager>(),
            services.GetRequiredService<IStaffMemberManager>(),
            services.GetRequiredService<ISession>(),
            services.GetRequiredService<IClock>(),
            Mock.Of<INotifier>(),
            Mock.Of<global::OrchardCore.DisplayManagement.IDisplayManager<CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels.AdherenceExceptionEditViewModel>>(),
            Mock.Of<global::OrchardCore.DisplayManagement.ModelBinding.IUpdateModelAccessor>(),
            Localizer<AdherenceExceptionsController>()), services, user);

    private static AdherenceController Adherence(IServiceProvider services, ClaimsPrincipal user)
        => WithContext(new AdherenceController(
            services.GetRequiredService<IAuthorizationService>(),
            services.GetRequiredService<AdherenceService>(),
            services.GetRequiredService<IWorkforceScopeService>(),
            services.GetRequiredService<ITeamManager>(),
            services.GetRequiredService<IStaffMemberManager>(),
            services.GetRequiredService<ISession>(),
            services.GetRequiredService<IClock>(),
            Mock.Of<INotifier>(),
            Localizer<AdherenceController>()), services, user);

    private static T WithContext<T>(T controller, IServiceProvider services, ClaimsPrincipal user)
        where T : Controller
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user, RequestServices = services },
        };
        controller.Url = Mock.Of<IUrlHelper>();
        controller.TempData = new TempDataDictionary(controller.HttpContext, Mock.Of<ITempDataProvider>());

        return controller;
    }

    private static IHtmlLocalizer<T> Localizer<T>()
    {
        var localizer = new Mock<IHtmlLocalizer<T>>();

        localizer.Setup(l => l[It.IsAny<string>()]).Returns((string name) => new LocalizedHtmlString(name, name));
        localizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string name, object[] _) => new LocalizedHtmlString(name, name));

        return localizer.Object;
    }

    private static async Task<Setup> SetupAsync()
    {
        var setup = new Setup();
        var agents = new Mock<IAgentProfileManager>();

        agents.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        setup.Harness = await WorkforceTests.CreateAsync(
            services =>
            {
                services.AddSingleton(agents.Object);
                services.AddSingleton(Mock.Of<IAgentStateHistory>());
                services.AddScoped<AgentWorkerService>();
                services.AddScoped<AdherenceRuleSetStore>();
                services.AddScoped<AdherenceRuleSetManager>();
                services.AddScoped<ICatalogEntryHandler<AdherenceRuleSet>, AdherenceRuleSetHandler>();
                services.AddScoped<AdherenceService>();
            },
            AdherenceIndexSchema.CreateAsync,
            [new AdherenceRuleSetIndexProvider(), new AdherenceExceptionIndexProvider(), new AdherenceDayIndexProvider(), new AdherenceAlertIndexProvider()]);

        var team = await WorkforceTests.AddTeamAsync(setup.Harness, "Support", TeamKind.Team);
        setup.Maya = await PlanningKit.AddMemberAsync(setup.Harness, "Maya", team, worker => worker.TeamMemberships[0].Role = TeamMemberRole.Manager);
        await PlanningKit.AddMemberAsync(setup.Harness, "Ben", team, worker => worker.TeamMemberships[0].Role = TeamMemberRole.Manager);

        return setup;
    }

    private sealed class Setup : IAsyncDisposable
    {
        public CalendarTestHarness Harness { get; set; }

        public StaffMember Maya { get; set; }

        public ValueTask DisposeAsync() => Harness?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
