using System.Security.Claims;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// Round 3: the published shift's own editor refuses a manager's change to their own shift and a change to a shift that
/// started without its permission, but editing the draft and publishing made the same change. The publish is now held to
/// the same rules.
/// </summary>
public sealed class PublishedShiftChangesPublishValidatorTests
{
    private static readonly DateTime _now = new(2026, 11, 27, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(ScheduleChangeKind.Moved)]
    [InlineData(ScheduleChangeKind.Removed)]
    [InlineData(ScheduleChangeKind.Added)]
    public async Task AManager_CantChangeOrGiveThemselvesAPublishedShift_ThroughTheDraft(ScheduleChangeKind kind)
    {
        // Arrange: the manager's own worker record is "worker-maya".
        var context = Context(new SchedulePublishChange { Kind = kind, ShiftKey = "shift-1", WorkerId = "worker-maya", PreviousWorkerId = kind == ScheduleChangeKind.Added ? null : "worker-maya", BeforeStartUtc = kind == ScheduleChangeKind.Added ? null : _now.AddDays(1) });

        // Act
        await Validator(ownWorkerId: "worker-maya", changesStarted: false).ValidateAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var violation = Assert.Single(context.Violations);
        Assert.Equal(LaborRuleEnforcement.Block, violation.Enforcement);
        Assert.Equal(["shift-1"], violation.ShiftKeys);
    }

    [Fact]
    public async Task AManager_CantReassignAShiftThatStarted_WithoutThePermission()
    {
        // Arrange
        var context = Context(new SchedulePublishChange { Kind = ScheduleChangeKind.Reassigned, ShiftKey = "shift-1", WorkerId = "worker-ben", PreviousWorkerId = "worker-sam", BeforeStartUtc = _now.AddHours(-2) });

        // Act
        await Validator(ownWorkerId: "worker-maya", changesStarted: false).ValidateAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(LaborRuleEnforcement.Block, Assert.Single(context.Violations).Enforcement);
    }

    [Fact]
    public async Task AManager_ChangesSomeoneElsesFutureShift_OrAStartedOneWithThePermission()
    {
        // Arrange
        var future = new SchedulePublishChange { Kind = ScheduleChangeKind.Moved, ShiftKey = "shift-1", WorkerId = "worker-ben", PreviousWorkerId = "worker-ben", BeforeStartUtc = _now.AddDays(1) };
        var started = new SchedulePublishChange { Kind = ScheduleChangeKind.Removed, ShiftKey = "shift-2", WorkerId = "worker-ben", BeforeStartUtc = _now.AddHours(-1) };
        var context = Context(future, started);

        // Act
        await Validator(ownWorkerId: "worker-maya", changesStarted: true).ValidateAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(context.Violations);
    }

    [Fact]
    public async Task APublishWithNoSignedInUser_IsNotChecked()
    {
        // Arrange
        var context = Context(new SchedulePublishChange { Kind = ScheduleChangeKind.Removed, ShiftKey = "shift-1", WorkerId = "worker-maya", BeforeStartUtc = _now.AddHours(-1) });

        // Act
        await Validator(ownWorkerId: "worker-maya", changesStarted: false, signedIn: false).ValidateAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(context.Violations);
    }

    private static SchedulePublishValidationContext Context(params SchedulePublishChange[] changes)
        => new() { Period = new SchedulePeriod { ItemId = "period-1", TeamId = "team-1" }, Changes = changes };

    private static PublishedShiftChangesPublishValidator Validator(string ownWorkerId, bool changesStarted, bool signedIn = true)
    {
        var user = signedIn
            ? new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-maya")], "Test"))
            : new ClaimsPrincipal(new ClaimsIdentity());

        var scopes = new Mock<IWorkforceScopeService>();
        scopes.Setup(scope => scope.CanDecideForWorkerAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClaimsPrincipal _, string workerId, CancellationToken _) => workerId != ownWorkerId);

        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(changesStarted ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(_now);

        return new PublishedShiftChangesPublishValidator(
            new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = user } },
            scopes.Object,
            authorization.Object,
            clock.Object,
            new PassThroughStringLocalizer<PublishedShiftChangesPublishValidator>());
    }
}
