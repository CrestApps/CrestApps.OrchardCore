using System.Security.Claims;
using CrestApps.OrchardCore.Skills.Core.ViewModels;
using CrestApps.OrchardCore.Skills.Drivers;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tests.Skills;

/// <summary>
/// The Skills section of the user editor, built on the shared <c>SkillSubjectSkillsDisplayDriverBase</c> that the worker
/// editor uses too: one proficiency row per enabled skill, shown only to those allowed to assign skills, and saved
/// under the edited record's subject.
/// </summary>
public sealed class SkillSubjectSkillsDisplayDriverTests
{
    [Fact]
    public async Task UserSection_ShowsTheEnabledSkills_WithWhatTheUserHolds()
    {
        // Arrange
        await using var harness = await SkillsTestHarness.CreateAsync();
        var gas = await harness.AddSkillAsync("Gas safety");
        await harness.AddSkillAsync("Old skill", skill => skill.Enabled = false);
        await harness.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>().AssignAsync(SkillSubject.User("user-1"), new SkillAssignmentInput { SkillId = gas.ItemId, Proficiency = 3 }, "admin"));

        // Act
        var model = await harness.RunAsync(async services =>
        {
            var result = await ((IDisplayDriver<User>)CreateUserDriver(services, canAssign: true)).BuildEditorAsync(new User { UserId = "user-1" }, PostedFormUpdateModel.CreateContext(null));

            return Assert.Single(await DisplayResultModels.BuildAsync<SubjectSkillsViewModel>(result));
        }, commit: false);

        // Assert
        var row = Assert.Single(model.Skills);

        Assert.Equal(gas.ItemId, row.SkillId);
        Assert.Equal(3, row.Proficiency);
    }

    [Fact]
    public async Task UserSection_IsHidden_FromThoseWhoCantAssignSkills_AndForAUserNotYetCreated()
    {
        // Arrange
        await using var harness = await SkillsTestHarness.CreateAsync();
        await harness.AddSkillAsync("Gas safety");

        // Act
        var (denied, unsaved) = await harness.RunAsync(async services =>
        {
            var deniedResult = await ((IDisplayDriver<User>)CreateUserDriver(services, canAssign: false)).BuildEditorAsync(new User { UserId = "user-1" }, PostedFormUpdateModel.CreateContext(null));
            var unsavedResult = await ((IDisplayDriver<User>)CreateUserDriver(services, canAssign: true)).BuildEditorAsync(new User(), PostedFormUpdateModel.CreateContext(null, isNew: true));

            return (deniedResult, unsavedResult);
        }, commit: false);

        // Assert
        Assert.Null(denied);
        Assert.Null(unsaved);
    }

    [Fact]
    public async Task UserSection_AssignsAndRemovesSkills_AndLeavesDisabledOnesAsHeld()
    {
        // Arrange
        await using var harness = await SkillsTestHarness.CreateAsync();
        var gas = await harness.AddSkillAsync("Gas safety");
        var water = await harness.AddSkillAsync("Water heaters");
        var old = await harness.AddSkillAsync("Old skill");
        var subject = SkillSubject.User("user-1");

        await harness.RunAsync(async services =>
        {
            var assignments = services.GetRequiredService<ISkillAssignmentService>();

            await assignments.AssignAsync(subject, new SkillAssignmentInput { SkillId = water.ItemId, Proficiency = 2 }, "admin");
            await assignments.AssignAsync(subject, new SkillAssignmentInput { SkillId = old.ItemId, Proficiency = 5 }, "admin");
        });
        await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ISkillManager>();
            var stored = await manager.FindByIdAsync(old.ItemId);

            stored.Enabled = false;
            await manager.UpdateAsync(stored);
        });

        var context = PostedFormUpdateModel.CreateContext(new SubjectSkillsViewModel
        {
            Skills =
            [
                new SubjectSkillRowViewModel { SkillId = gas.ItemId, Proficiency = 4 },
                new SubjectSkillRowViewModel { SkillId = water.ItemId, Proficiency = 0 },
                new SubjectSkillRowViewModel { SkillId = old.ItemId, Proficiency = 0 },
            ],
        });

        // Act
        await harness.RunAsync(services => ((IDisplayDriver<User>)CreateUserDriver(services, canAssign: true)).UpdateEditorAsync(new User { UserId = "user-1" }, context));
        var held = await harness.RunAsync(services => services.GetRequiredService<ISkillAssignmentService>().GetAssignmentsAsync(subject), commit: false);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
        Assert.Equal(
            [(gas.ItemId, 4), (old.ItemId, 5)],
            held.OrderBy(assignment => assignment.Proficiency).Select(assignment => (assignment.SkillId, assignment.Proficiency)));
    }

    private static UserSkillsDisplayDriver CreateUserDriver(IServiceProvider services, bool canAssign)
    {
        var authorizationService = new Mock<IAuthorizationService>();

        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(canAssign ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin")], "Test")) },
        };

        return new UserSkillsDisplayDriver(
            services.GetRequiredService<ISkillManager>(),
            services.GetRequiredService<ISkillAssignmentService>(),
            httpContextAccessor,
            authorizationService.Object);
    }
}
