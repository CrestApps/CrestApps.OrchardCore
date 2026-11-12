using System.Security.Claims;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Skills;
using CrestApps.OrchardCore.Skills.Core.ViewModels;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Staff.Drivers;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Staff.ViewModels;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Workforce;
using CrestApps.OrchardCore.Workforce.Drivers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Staff;

/// <summary>
/// The staff editor is built by display drivers: Staff's own driver fills and reads the person's fields, with its checks
/// reported under its prefix; Skills adds its card; Workforce adds its scheduling card; and a driver another module
/// registers adds its own section.
/// </summary>
public sealed class StaffEditorDriverTests
{
    [Fact]
    public async Task StaffDriver_FillsTheEditorFromTheRecord_WithEmptyTeamRows_AndHidesTheBirthDateFromThoseWhoCantSeeIt()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var team = await WorkforceTests.AddTeamAsync(harness, "Plumbers", TeamKind.Team);
        var staff = new StaffMember
        {
            DisplayName = "Lee Park",
            JobTitle = "Plumber",
            Status = StaffStatus.OnLeave,
            DateOfBirth = new DateOnly(1990, 5, 1),
            TeamMemberships = [new TeamMembership { TeamId = team.ItemId, Role = TeamMemberRole.Lead }],
        };

        // Act
        var (allowed, denied) = await harness.RunAsync(async services =>
        {
            var allowedModel = await BuildAsync(CreateStaffDriver(services, Grants.All), staff);
            var deniedModel = await BuildAsync(CreateStaffDriver(services, [StaffPermissions.ManageStaff.Name]), staff);

            return (allowedModel, deniedModel);
        }, commit: false);

        // Assert
        Assert.Equal("Lee Park", allowed.DisplayName);
        Assert.Equal("Plumber", allowed.JobTitle);
        Assert.Equal(nameof(StaffStatus.OnLeave), allowed.Status);
        Assert.Equal([team.ItemId, null, null], allowed.Memberships.Select(row => row.TeamId));
        Assert.Equal(nameof(TeamMemberRole.Lead), allowed.Memberships[0].Role);
        Assert.Equal([team.ItemId], allowed.TeamOptions.Select(option => option.Value));
        Assert.True(allowed.CanSeeSensitive);
        Assert.Equal(new DateOnly(1990, 5, 1), allowed.DateOfBirth);
        Assert.False(denied.CanSeeSensitive);
        Assert.Null(denied.DateOfBirth);
    }

    [Fact]
    public async Task StaffDriver_AppliesThePostedFields_KeepsTheBirthDateOfThoseWhoCantSeeIt_AndOnlyRegisteredCapabilities()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync(services => services.AddStaffCapability("Technician", new("Technician")));
        var team = await WorkforceTests.AddTeamAsync(harness, "Plumbers", TeamKind.Team);
        var staff = new StaffMember
        {
            DateOfBirth = new DateOnly(1990, 5, 1),
            HomeBase = new StaffLocation { Address = "1 Main St", Latitude = 1, Longitude = 2 },
            Capabilities = ["SetByAnotherModule"],
        };
        var posted = new StaffEditViewModel
        {
            DisplayName = "Lee Park",
            EmploymentType = nameof(EmploymentType.PartTime),
            Status = "Nonsense",
            DateOfBirth = new DateOnly(2001, 1, 1),
            HomeBase = " 2 Main St ",
            WorkLocation = "Depot",
            Capabilities = ["Technician", "Made up"],
            Memberships =
            [
                new TeamMembershipRowViewModel { TeamId = team.ItemId, Role = nameof(TeamMemberRole.Scheduler) },
                new TeamMembershipRowViewModel(),
            ],
        };
        var context = PostedFormUpdateModel.CreateContext(posted);

        // Act
        await harness.RunAsync(services => ((IDisplayDriver<StaffMember>)CreateStaffDriver(services, [StaffPermissions.ManageStaff.Name])).UpdateEditorAsync(staff, context), commit: false);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
        Assert.Equal("Lee Park", staff.DisplayName);
        Assert.Equal(EmploymentType.PartTime, staff.EmploymentType);
        Assert.Equal(StaffStatus.Active, staff.Status);
        Assert.Equal(new DateOnly(1990, 5, 1), staff.DateOfBirth);
        Assert.Equal("2 Main St", staff.HomeBase.Address);
        Assert.Null(staff.HomeBase.Latitude);
        Assert.Equal("Depot", staff.WorkLocation);
        Assert.Equal(["SetByAnotherModule", "Technician"], staff.Capabilities);
        Assert.Equal(team.ItemId, Assert.Single(staff.TeamMemberships).TeamId);
        Assert.Equal(TeamMemberRole.Scheduler, staff.TeamMemberships[0].Role);
        Assert.Equal(team.ItemId, staff.PrimaryTeamId);
    }

    [Fact]
    public async Task StaffDriver_RefusesTeamsTheManagerDoesntManage_UnderItsPrefix()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var managed = await WorkforceTests.AddTeamAsync(harness, "Plumbers", TeamKind.Team);
        var other = await WorkforceTests.AddTeamAsync(harness, "Electricians", TeamKind.Team);
        var posted = new StaffEditViewModel { DisplayName = "Lee Park", Memberships = [new TeamMembershipRowViewModel { TeamId = other.ItemId }] };
        var context = PostedFormUpdateModel.CreateContext(posted, isNew: true);
        var scope = new StaffScope(false, new HashSet<string>(StringComparer.Ordinal) { managed.ItemId }, new HashSet<string>(StringComparer.Ordinal));

        // Act
        var model = await harness.RunAsync(async services =>
        {
            var result = await ((IDisplayDriver<StaffMember>)CreateStaffDriver(services, Grants.All, scope)).UpdateEditorAsync(new StaffMember(), context);

            return Assert.Single(await DisplayResultModels.BuildAsync<StaffEditViewModel>(result));
        }, commit: false);

        // Assert
        Assert.Equal(["You can only add people to the teams you manage."], context.Updater.ModelState["StaffMember.Memberships"].Errors.Select(error => error.ErrorMessage));
        Assert.Equal([managed.ItemId], model.TeamOptions.Select(option => option.Value));
    }

    [Fact]
    public void TheBuiltInDrivers_AreRegistered()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        new global::CrestApps.OrchardCore.Staff.Startup().ConfigureServices(services);
        new global::CrestApps.OrchardCore.Staff.SkillsStartup().ConfigureServices(services);
        new global::CrestApps.OrchardCore.Workforce.Startup().ConfigureServices(services);

        // Assert
        var drivers = services.Where(descriptor => descriptor.ServiceType == typeof(IDisplayDriver<StaffMember>)).Select(descriptor => descriptor.ImplementationType).ToList();

        Assert.Contains(typeof(StaffMemberDisplayDriver), drivers);
        Assert.Contains(typeof(StaffSkillsDisplayDriver), drivers);
        Assert.Contains(typeof(WorkforceStaffDisplayDriver), drivers);
    }

    [Fact]
    public async Task Editor_ShowsTheStaffFields_ASectionAnotherModuleRegisters_AndTheSkillsCard_InPlacementOrder()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync(services => services
            .AddEditorDisplayManagement()
            .AddScoped<IDisplayDriver<StaffMember>>(provider => CreateStaffDriver(provider, Grants.All))
            .AddScoped<IDisplayDriver<StaffMember>>(provider => CreateSkillsDriver(provider))
            .AddDisplayDriver<StaffMember, ExtraSectionDisplayDriver<StaffMember>>());
        await AddSkillAsync(harness, "Gas safety");

        // Act
        var shapes = await harness.RunAsync(async services =>
        {
            var editor = await services.GetRequiredService<IDisplayManager<StaffMember>>().BuildEditorAsync(new StaffMember { ItemId = "staff-1", DisplayName = "Lee Park" }, new PostedFormUpdateModel(null), isNew: true);

            return EditorDisplayManagement.ZoneShapes(editor);
        }, commit: false);

        // Assert
        Assert.Equal(["StaffMemberFields_Edit", ExtraSectionDisplayDriver<StaffMember>.ShapeType, "StaffMemberSkills_Edit"], shapes.Select(shape => shape.Metadata.Type));
        Assert.Equal("Lee Park", Assert.IsAssignableFrom<StaffEditViewModel>(shapes[0]).DisplayName);
        Assert.Equal(nameof(StaffMember), shapes[0].Metadata.Prefix);
        Assert.Equal(["Gas safety"], Assert.IsAssignableFrom<SubjectSkillsViewModel>(shapes[2]).Skills.Select(skill => skill.Name));
    }

    [Fact]
    public async Task Editor_SavesTheSkillsOfANewStaffMember_AndReportsAnotherModulesError_UnderItsPrefix()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync(services => services
            .AddEditorDisplayManagement()
            .AddScoped<IDisplayDriver<StaffMember>>(provider => CreateSkillsDriver(provider))
            .AddDisplayDriver<StaffMember, RefusingSectionDisplayDriver<StaffMember>>());
        var skill = await AddSkillAsync(harness, "Gas safety");
        var updater = new PostedFormUpdateModel(new SubjectSkillsViewModel { Skills = [new SubjectSkillRowViewModel { SkillId = skill.ItemId, Proficiency = 4 }] });

        // Act
        var held = await harness.RunAsync(async services =>
        {
            await services.GetRequiredService<IDisplayManager<StaffMember>>().UpdateEditorAsync(new StaffMember { ItemId = "staff-1" }, updater, isNew: true);

            return await services.GetRequiredService<ISkillAssignmentService>().GetAssignmentsAsync(SkillSubject.Staff("staff-1"));
        }, commit: false);

        // Assert
        Assert.Equal(4, Assert.Single(held).Proficiency);
        Assert.Equal([RefusingSectionDisplayDriver<StaffMember>.Error], updater.ModelState["StaffMember.Note"].Errors.Select(error => error.ErrorMessage));
    }

    private static async Task<StaffEditViewModel> BuildAsync(IDisplayDriver<StaffMember> driver, StaffMember staff)
    {
        var result = await driver.BuildEditorAsync(staff, PostedFormUpdateModel.CreateContext(null));

        return Assert.Single(await DisplayResultModels.BuildAsync<StaffEditViewModel>(result));
    }

    private static StaffMemberDisplayDriver CreateStaffDriver(IServiceProvider services, string[] grants, StaffScope scope = null)
    {
        var scopes = new Mock<IStaffScopeService>();

        scopes.Setup(service => service.GetScopeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>())).ReturnsAsync(scope ?? StaffScope.Everyone);

        return new StaffMemberDisplayDriver(
            Authorization(grants),
            CreateHttpContextAccessor(services),
            scopes.Object,
            services.GetRequiredService<IStaffDirectory>(),
            services.GetRequiredService<ITeamManager>(),
            Mock.Of<ITimeZoneSelectListProvider>(),
            services.GetRequiredService<IClock>(),
            new SourceTextLocalizerFactory(),
            services.GetRequiredService<IStringLocalizer<StaffMemberDisplayDriver>>());
    }

    private static StaffSkillsDisplayDriver CreateSkillsDriver(IServiceProvider services)
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
        => new()
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
            StaffPermissions.ManageStaff.Name,
            StaffPermissions.ViewSensitiveStaffData.Name,
            SkillsPermissions.AssignSkills.Name,
        ];
    }
}
