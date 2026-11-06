using System.Security.Claims;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Skills;
using CrestApps.OrchardCore.Skills.Core.ViewModels;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Drivers;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// The worker editor is built by display drivers: the worker's own driver fills and reads the fields the form always
/// had, with its checks reported under its prefix; Skills adds its card through the shared skills section; and a driver
/// another module registers adds its own section.
/// </summary>
public sealed class WorkerEditorDriverTests
{
    [Fact]
    public async Task ProfileDriver_FillsTheEditorFromTheWorker_WithEmptyTeamRows()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var team = await WorkforceTests.AddTeamAsync(harness, "Plumbers", TeamKind.Team);
        var worker = new WorkerProfile
        {
            DisplayName = "Lee Park",
            JobTitle = "Plumber",
            Status = WorkerStatus.OnLeave,
            DateOfBirth = new DateOnly(1990, 5, 1),
            TeamMemberships = [new TeamMembership { TeamId = team.ItemId, Role = TeamMemberRole.Lead }],
        };

        // Act
        var (allowed, denied) = await harness.RunAsync(async services =>
        {
            var allowedModel = await BuildAsync(CreateProfileDriver(services, Grants.All), worker);
            var deniedModel = await BuildAsync(CreateProfileDriver(services, [WorkforcePermissions.ManageWorkers.Name]), worker);

            return (allowedModel, deniedModel);
        }, commit: false);

        // Assert
        Assert.Equal("Lee Park", allowed.DisplayName);
        Assert.Equal("Plumber", allowed.JobTitle);
        Assert.Equal(nameof(WorkerStatus.OnLeave), allowed.Status);
        Assert.Equal([team.ItemId, null, null, null], allowed.Memberships.Select(row => row.TeamId));
        Assert.Equal(nameof(TeamMemberRole.Lead), allowed.Memberships[0].Role);
        Assert.Equal([team.ItemId], allowed.TeamOptions.Select(option => option.Value));
        Assert.True(allowed.CanSeeSensitive);
        Assert.Equal(new DateOnly(1990, 5, 1), allowed.DateOfBirth);
        Assert.False(denied.CanSeeSensitive);
        Assert.Null(denied.DateOfBirth);
    }

    [Fact]
    public async Task ProfileDriver_AppliesThePostedFields_AndKeepsTheBirthDateOfThoseWhoCantSeeIt()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var team = await WorkforceTests.AddTeamAsync(harness, "Plumbers", TeamKind.Team);
        var worker = new WorkerProfile { DateOfBirth = new DateOnly(1990, 5, 1), HomeBase = new WorkforceLocation { Address = "1 Main St", Latitude = 1, Longitude = 2 } };
        var posted = new WorkerEditViewModel
        {
            DisplayName = "Lee Park",
            EmploymentType = nameof(EmploymentType.PartTime),
            Status = "Nonsense",
            DateOfBirth = new DateOnly(2001, 1, 1),
            HomeBase = " 2 Main St ",
            Availability = "Monday 09:00-17:00",
            Memberships =
            [
                new TeamMembershipRowViewModel { TeamId = team.ItemId, Role = nameof(TeamMemberRole.Scheduler) },
                new TeamMembershipRowViewModel(),
            ],
        };
        var context = PostedFormUpdateModel.CreateContext(posted);

        // Act
        await harness.RunAsync(services => Update(CreateProfileDriver(services, [WorkforcePermissions.ManageWorkers.Name]), worker, context), commit: false);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
        Assert.Equal("Lee Park", worker.DisplayName);
        Assert.Equal(EmploymentType.PartTime, worker.EmploymentType);
        Assert.Equal(WorkerStatus.Active, worker.Status);
        Assert.Equal(new DateOnly(1990, 5, 1), worker.DateOfBirth);
        Assert.Equal("2 Main St", worker.HomeBase.Address);
        Assert.Null(worker.HomeBase.Latitude);
        Assert.Equal(DayOfWeek.Monday, Assert.Single(worker.Availability).Day);
        Assert.Equal(team.ItemId, Assert.Single(worker.TeamMemberships).TeamId);
        Assert.Equal(TeamMemberRole.Scheduler, worker.TeamMemberships[0].Role);
        Assert.Equal(team.ItemId, worker.PrimaryTeamId);
    }

    [Fact]
    public async Task ProfileDriver_ReportsItsErrorsUnderItsPrefix_AndShowsWhatWasTyped()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var managed = await WorkforceTests.AddTeamAsync(harness, "Plumbers", TeamKind.Team);
        var other = await WorkforceTests.AddTeamAsync(harness, "Electricians", TeamKind.Team);
        var posted = new WorkerEditViewModel
        {
            DisplayName = "Lee Park",
            Availability = "Someday whenever",
            Memberships = [new TeamMembershipRowViewModel { TeamId = other.ItemId }],
        };
        var context = PostedFormUpdateModel.CreateContext(posted, isNew: true);
        var scope = new WorkforceScope(false, new HashSet<string>(StringComparer.Ordinal) { managed.ItemId });

        // Act
        var model = await harness.RunAsync(async services =>
        {
            var result = await Update(CreateProfileDriver(services, Grants.All, scope), new WorkerProfile(), context);

            return Assert.Single(await DisplayResultModels.BuildAsync<WorkerEditViewModel>(result));
        }, commit: false);

        // Assert
        Assert.StartsWith("This line can't be read:", Assert.Single(context.Updater.ModelState["WorkerProfile.Availability"].Errors).ErrorMessage);
        Assert.Equal(["You can only add workers to the teams you manage."], context.Updater.ModelState["WorkerProfile.Memberships"].Errors.Select(error => error.ErrorMessage));
        Assert.Equal("Someday whenever", model.Availability);
        Assert.Equal([managed.ItemId], model.TeamOptions.Select(option => option.Value));
    }

    [Fact]
    public void TheBuiltInDrivers_AreRegistered()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        new global::CrestApps.OrchardCore.Workforce.Startup().ConfigureServices(services);

        // Assert
        var drivers = services.Where(descriptor => descriptor.ServiceType == typeof(IDisplayDriver<WorkerProfile>)).Select(descriptor => descriptor.ImplementationType).ToList();

        Assert.Contains(typeof(WorkerProfileDisplayDriver), drivers);
        Assert.Contains(typeof(WorkerSkillsDisplayDriver), drivers);
    }

    [Fact]
    public async Task Editor_ShowsTheProfile_ASectionAnotherModuleRegisters_AndTheSkillsCard_InPlacementOrder()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync(services => services
            .AddEditorDisplayManagement()
            .AddScoped<IDisplayDriver<WorkerProfile>>(provider => CreateProfileDriver(provider, Grants.All))
            .AddScoped<IDisplayDriver<WorkerProfile>>(provider => CreateSkillsDriver(provider))
            .AddDisplayDriver<WorkerProfile, ExtraSectionDisplayDriver<WorkerProfile>>());
        await AddSkillAsync(harness, "Gas safety");

        // Act
        var shapes = await harness.RunAsync(async services =>
        {
            var editor = await services.GetRequiredService<IDisplayManager<WorkerProfile>>().BuildEditorAsync(new WorkerProfile { ItemId = "worker-1", DisplayName = "Lee Park" }, new PostedFormUpdateModel(null), isNew: true);

            return EditorDisplayManagement.ZoneShapes(editor);
        }, commit: false);

        // Assert
        Assert.Equal(["WorkerProfileFields_Edit", ExtraSectionDisplayDriver<WorkerProfile>.ShapeType, "WorkerSkills_Edit"], shapes.Select(shape => shape.Metadata.Type));
        Assert.Equal("Lee Park", Assert.IsAssignableFrom<WorkerEditViewModel>(shapes[0]).DisplayName);
        Assert.Equal(nameof(WorkerProfile), shapes[0].Metadata.Prefix);
        Assert.Equal(["Gas safety"], Assert.IsAssignableFrom<SubjectSkillsViewModel>(shapes[2]).Skills.Select(skill => skill.Name));
    }

    [Fact]
    public async Task Editor_SavesTheSkillsOfANewWorker_AndReportsAnotherModulesError_UnderItsPrefix()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync(services => services
            .AddEditorDisplayManagement()
            .AddScoped<IDisplayDriver<WorkerProfile>>(provider => CreateSkillsDriver(provider))
            .AddDisplayDriver<WorkerProfile, RefusingSectionDisplayDriver<WorkerProfile>>());
        var skill = await AddSkillAsync(harness, "Gas safety");
        var updater = new PostedFormUpdateModel(new SubjectSkillsViewModel { Skills = [new SubjectSkillRowViewModel { SkillId = skill.ItemId, Proficiency = 4 }] });

        // Act
        var held = await harness.RunAsync(async services =>
        {
            await services.GetRequiredService<IDisplayManager<WorkerProfile>>().UpdateEditorAsync(new WorkerProfile { ItemId = "worker-1" }, updater, isNew: true);

            return await services.GetRequiredService<ISkillAssignmentService>().GetAssignmentsAsync(SkillSubject.Worker("worker-1"));
        }, commit: false);

        // Assert
        Assert.Equal(4, Assert.Single(held).Proficiency);
        Assert.Equal([RefusingSectionDisplayDriver<WorkerProfile>.Error], updater.ModelState["WorkerProfile.Note"].Errors.Select(error => error.ErrorMessage));
    }

    private static async Task<WorkerEditViewModel> BuildAsync(IDisplayDriver<WorkerProfile> driver, WorkerProfile worker)
    {
        var result = await driver.BuildEditorAsync(worker, PostedFormUpdateModel.CreateContext(null));

        return Assert.Single(await DisplayResultModels.BuildAsync<WorkerEditViewModel>(result));
    }

    private static Task<global::OrchardCore.DisplayManagement.Views.IDisplayResult> Update(IDisplayDriver<WorkerProfile> driver, WorkerProfile worker, UpdateEditorContext context)
        => driver.UpdateEditorAsync(worker, context);

    private static WorkerProfileDisplayDriver CreateProfileDriver(IServiceProvider services, string[] grants, WorkforceScope scope = null)
    {
        var scopes = new Mock<IWorkforceScopeService>();

        scopes.Setup(service => service.GetManagedScopeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>())).ReturnsAsync(scope ?? WorkforceScope.Everything);

        return new WorkerProfileDisplayDriver(
            Authorization(grants),
            CreateHttpContextAccessor(services),
            scopes.Object,
            services.GetRequiredService<ITeamManager>(),
            Mock.Of<ITimeZoneSelectListProvider>(),
            services.GetRequiredService<IStringLocalizer<WorkerProfileDisplayDriver>>());
    }

    private static WorkerSkillsDisplayDriver CreateSkillsDriver(IServiceProvider services)
        => new(
            services.GetRequiredService<ISkillManager>(),
            services.GetRequiredService<ISkillAssignmentService>(),
            CreateHttpContextAccessor(services),
            Authorization(Grants.All));

    private static Task<Skill> AddSkillAsync(CalendarTestHarness harness, string name)
        => harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ISkillManager>();
            var skill = await manager.NewAsync();

            skill.Name = name;
            await manager.CreateAsync(skill);

            return skill;
        });

    private static HttpContextAccessor CreateHttpContextAccessor(IServiceProvider services)
        => new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = services,
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin")], "Test")),
            },
        };

    private static IAuthorizationService Authorization(string[] grants)
    {
        var authorizationService = new Mock<IAuthorizationService>();

        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                requirements.OfType<PermissionRequirement>().All(requirement => grants.Contains(requirement.Permission.Name))
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        return authorizationService.Object;
    }

    private static class Grants
    {
        public static readonly string[] All =
        [
            WorkforcePermissions.ManageWorkers.Name,
            WorkforcePermissions.ViewSensitiveWorkerData.Name,
            SkillsPermissions.AssignSkills.Name,
        ];
    }
}
