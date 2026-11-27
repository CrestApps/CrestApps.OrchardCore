using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Approvals;
using CrestApps.OrchardCore.Approvals.Core.Services;
using CrestApps.OrchardCore.Approvals.Controllers;
using CrestApps.OrchardCore.Approvals.Deployments;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Recipes;
using CrestApps.OrchardCore.Approvals.ViewModels;
using CrestApps.OrchardCore.Approvals.Workflows;
using CrestApps.OrchardCore.Core.SourceAccess;
using CrestApps.OrchardCore.SourceAccess;
using CrestApps.OrchardCore.Tests.Checks;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Recipes.Models;
using OrchardCore.Security.Permissions;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Tests.Approvals;

/// <summary>
/// Requests about a subject are seen and acted on only by those its subject kind allows, on the pages, the export and
/// the workflow tasks; the permissions guard every path; and a workflow decides only as a signed-in person.
/// </summary>
public sealed class ApprovalsAuthorizationTests
{
    private const string PayRun = "PayRun";

    [Fact]
    public async Task Decide_ARequestThePersonMayNeitherSeeNorDecide_IsNotFound()
    {
        // Arrange: Sam may view requests, but not group A's, and no policy names Sam.
        var (context, _, groupA) = await SeedAsync();
        var controller = Controller(context, "sam", grants: [ApprovalsPermissions.ViewApprovalRequests, ApprovalsPermissions.ManageApprovalRequests]);

        // Act + Assert
        Assert.IsType<NotFoundResult>(await controller.Decide(groupA.ItemId));
        Assert.IsType<NotFoundResult>(await controller.DecidePost(groupA.ItemId, new ApprovalDecideViewModel(), "Approve"));
        Assert.IsType<NotFoundResult>(await controller.Cancel(groupA.ItemId, null));
        Assert.Equal(ApprovalStatus.Pending, groupA.Status);
    }

    [Fact]
    public async Task DecidePost_ByAnApproverWhoMaySeeTheSubject_Approves()
    {
        // Arrange
        var (context, _, groupA) = await SeedAsync();
        var controller = Controller(context, "pat");

        // Act
        var result = await controller.DecidePost(groupA.ItemId, new ApprovalDecideViewModel(), "Approve");

        // Assert
        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(ApprovalStatus.Approved, groupA.Status);
    }

    [Fact]
    public async Task AManagerWithoutApprovalsPermissions_DecidesTheirTeamsRequest_AndGetsNotFoundForOthers()
    {
        // Arrange: Morgan holds no Approvals permission; the time sheet policy names Morgan as the team's manager.
        var (context, open, groupA) = await SeedAsync();
        var timesheet = TimesheetPolicy(context);
        var team = (await context.Service.RequestAsync(new ApprovalSubmission { Policy = timesheet.Name, SubjectKind = "Timesheet", SubjectId = "t-1", Title = "Week 46" }, TestSecurity.User("riley"), TestContext.Current.CancellationToken)).Request;
        var morgan = Controller(context, "morgan", grants: []);

        // Act
        var page = await morgan.Decide(team.ItemId);
        var others = new[] { await morgan.Decide(open.ItemId), await morgan.Decide(groupA.ItemId), await morgan.DecidePost(groupA.ItemId, new ApprovalDecideViewModel(), "Approve") };
        var decided = await morgan.DecidePost(team.ItemId, new ApprovalDecideViewModel(), "Approve");

        // Assert
        Assert.IsType<ViewResult>(page);
        Assert.All(others, result => Assert.IsType<NotFoundResult>(result));
        Assert.IsType<RedirectToActionResult>(decided);
        Assert.Equal(ApprovalStatus.Approved, team.Status);
        Assert.Equal(ApprovalStatus.Pending, groupA.Status);

        // Having decided it, Morgan still sees the request, though no longer named to decide it.
        Assert.IsType<ViewResult>(await morgan.Decide(team.ItemId));
    }

    [Fact]
    public async Task ARequesterWithoutApprovalsPermissions_SeesAndWithdrawsTheirOwnRequest_Only()
    {
        // Arrange: Jo, whom no policy names, asked for one request; Riley asked for the open one.
        var (context, open, _) = await SeedAsync();
        var jos = (await context.Service.RequestAsync(new ApprovalSubmission { Policy = "PayRunApproval", SubjectKind = "Expense", SubjectId = "e-2", Title = "Jo's expense" }, TestSecurity.User("jo"), TestContext.Current.CancellationToken)).Request;
        var jo = Controller(context, "jo", grants: []);

        // Act
        var own = await jo.Decide(jos.ItemId);
        var other = await jo.Decide(open.ItemId);
        var withdrawOther = await jo.Cancel(open.ItemId, null);
        var withdrawn = await jo.Cancel(jos.ItemId, "Not needed");

        // Assert
        Assert.IsType<ViewResult>(own);
        Assert.IsType<NotFoundResult>(other);
        Assert.IsType<NotFoundResult>(withdrawOther);
        Assert.Equal(ApprovalStatus.Pending, open.Status);
        Assert.IsType<RedirectToActionResult>(withdrawn);
        Assert.Equal(ApprovalStatus.Canceled, jos.Status);
    }

    [Fact]
    public async Task ANamedApproverWhoMayNotActOnTheRecord_IsRefused_AndNotListed()
    {
        // Arrange: the Finance rule names Lee, but the pay runs of group A are only Pat's to act on.
        var context = new ApprovalsTestContext(Access());
        context.Policy();
        var groupA = await context.RequestAsync(subjectId: "run-a", scope: "groupA");
        var inbox = new ApprovalInbox(context.Store, context.Service, context.Access);
        var lee = Controller(context, "lee", grants: []);

        // Act
        var leeCanDecide = await context.Service.GetEligibilityAsync(groupA, TestSecurity.User("lee"), TestContext.Current.CancellationToken);
        var patCanDecide = await context.Service.GetEligibilityAsync(groupA, TestSecurity.User("pat"), TestContext.Current.CancellationToken);
        var (leeCount, _) = await inbox.ListWaitingAsync(TestSecurity.User("lee"), null, 0, 10, TestContext.Current.CancellationToken);
        var (patCount, _) = await inbox.ListWaitingAsync(TestSecurity.User("pat"), null, 0, 10, TestContext.Current.CancellationToken);
        var decided = await context.Service.DecideAsync(groupA, TestSecurity.User("lee"), ApprovalDecisionKind.Approve, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(leeCanDecide.CanDecide);
        Assert.True(patCanDecide.CanDecide);
        Assert.Equal(0, leeCount);
        Assert.Equal(1, patCount);
        Assert.False(decided.Succeeded);
        Assert.IsType<NotFoundResult>(await lee.Decide(groupA.ItemId));
        Assert.Equal(ApprovalStatus.Pending, groupA.Status);
    }

    [Fact]
    public async Task WaitingForMe_CountsEveryPendingRequest_BeyondOneBatch()
    {
        // Arrange: more pending requests than one batch reads.
        var context = new ApprovalsTestContext();
        context.Policy();

        for (var i = 0; i < ApprovalInbox.BatchSize + 30; i++)
        {
            await context.RequestAsync(subjectId: "run-" + i);
        }

        var inbox = new ApprovalInbox(context.Store, context.Service, context.Access);

        // Act
        var (count, page) = await inbox.ListWaitingAsync(TestSecurity.User("pat"), null, ApprovalInbox.BatchSize, 50, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ApprovalInbox.BatchSize + 30, count);
        Assert.Equal(30, page.Count);
    }

    [Fact]
    public async Task WaitingForMe_ListsOnlyTheRequestsWhoseCurrentStageNamesThePerson()
    {
        // Arrange
        var (context, open, groupA) = await SeedAsync();
        var timesheet = TimesheetPolicy(context);
        var team = (await context.Service.RequestAsync(new ApprovalSubmission { Policy = timesheet.Name, SubjectKind = "Timesheet", SubjectId = "t-1", Title = "Week 46" }, TestSecurity.User("riley"), TestContext.Current.CancellationToken)).Request;
        var inbox = new ApprovalInbox(context.Store, context.Service, context.Access);

        // Act
        var (morganCount, morgans) = await inbox.ListWaitingAsync(TestSecurity.User("morgan"), null, 0, 10, TestContext.Current.CancellationToken);
        var (patCount, pats) = await inbox.ListWaitingAsync(TestSecurity.User("pat"), null, 0, 10, TestContext.Current.CancellationToken);
        var (_, patsTimesheets) = await inbox.ListWaitingAsync(TestSecurity.User("pat"), "Timesheet", 0, 10, TestContext.Current.CancellationToken);
        var (_, rileys) = await inbox.ListWaitingAsync(TestSecurity.User("riley"), null, 0, 10, TestContext.Current.CancellationToken);

        // Assert: the requester decides none of their own.
        Assert.Equal(1, morganCount);
        Assert.Equal([team.ItemId], morgans.Select(request => request.ItemId));
        Assert.Equal(2, patCount);
        Assert.Equal(new[] { open.ItemId, groupA.ItemId }.Order(StringComparer.Ordinal), pats.Select(request => request.ItemId).Order(StringComparer.Ordinal));
        Assert.Empty(patsTimesheets);
        Assert.Empty(rileys);
    }

    [Fact]
    public async Task Cancel_BySomeoneOtherThanTheRequester_WithoutManage_IsForbidden_ButTheRequesterMay()
    {
        // Arrange
        var (context, open, _) = await SeedAsync();
        var stranger = Controller(context, "kim", grants: [ApprovalsPermissions.ViewApprovalRequests]);
        var requester = Controller(context, "riley", grants: [ApprovalsPermissions.ViewApprovalRequests]);

        // Act
        var refused = await stranger.Cancel(open.ItemId, null);
        var statusAfterRefusal = open.Status;
        var withdrawn = await requester.Cancel(open.ItemId, "Not needed");

        // Assert
        Assert.IsType<ForbidResult>(refused);
        Assert.Equal(ApprovalStatus.Pending, statusAfterRefusal);
        Assert.IsType<RedirectToActionResult>(withdrawn);
        Assert.Equal(ApprovalStatus.Canceled, open.Status);
    }

    [Fact]
    public async Task DecidePost_ByTheRequester_ShowsWhy_AndRecordsNothing()
    {
        // Arrange
        var (context, open, _) = await SeedAsync();
        var controller = Controller(context, "riley");

        // Act
        var result = await controller.DecidePost(open.ItemId, new ApprovalDecideViewModel(), "Approve");

        // Assert
        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Empty(open.Decisions);
    }

    [Fact]
    public async Task Export_WritesOnlyTheRequestsAboutSubjectsThePersonMaySee()
    {
        // Arrange
        var (context, open, groupA) = await SeedAsync();

        // Act
        var lee = await RecipeRoundTrip.ExportAsync(ExportSource(context, Accessor("lee"), new RecordingNotifier(), ApprovalsPermissions.ViewApprovalRequests), new ApprovalRequestsDeploymentStep());
        var pat = await RecipeRoundTrip.ExportAsync(ExportSource(context, Accessor("pat"), new RecordingNotifier(), ApprovalsPermissions.ViewApprovalRequests), new ApprovalRequestsDeploymentStep());

        // Assert
        Assert.Equal([open.ItemId], Ids(lee));
        Assert.Equal(new[] { open.ItemId, groupA.ItemId }.Order(StringComparer.Ordinal), Ids(pat).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Export_RunWithoutASignedInPerson_WritesOnlyTheRequestsNoSubjectKindRestricts()
    {
        // Arrange
        var (context, open, _) = await SeedAsync();

        // Act
        var step = await RecipeRoundTrip.ExportAsync(ExportSource(context, new HttpContextAccessor(), new RecordingNotifier()), new ApprovalRequestsDeploymentStep());

        // Assert
        Assert.Equal([open.ItemId], Ids(step));
    }

    [Fact]
    public async Task Export_RunByAPersonWhoMayNotView_WritesNothing_AndSaysSo()
    {
        // Arrange
        var (context, _, _) = await SeedAsync();
        var notifier = new RecordingNotifier();
        var result = new DeploymentPlanResult(Mock.Of<IFileBuilder>(), new RecipeDescriptor());

        // Act
        await ExportSource(context, Accessor("pat"), notifier).ProcessDeploymentStepAsync(new ApprovalRequestsDeploymentStep(), result);

        // Assert
        Assert.Empty(result.Steps);
        Assert.Contains(notifier.Entries, entry => entry.Type == NotifyType.Warning);
    }

    [Fact]
    public async Task ApproveTask_ForARequestTheWorkflowsPersonMayNotSee_IsNotFound()
    {
        // Arrange
        var (context, _, groupA) = await SeedAsync();
        var task = new ApproveRequestTask(context.Manager, context.Service, Access(), Accessor("lee"), Evaluator(), RecipeRoundTrip.Localizer<ApproveRequestTask>()) { RequestId = groupA.ItemId };

        // Act
        var result = await task.ExecuteAsync(WorkflowContext(), new ActivityContext { Activity = task });

        // Assert
        Assert.Equal(["NotFound"], result.Outcomes);
        Assert.Equal(ApprovalStatus.Pending, groupA.Status);
    }

    [Fact]
    public async Task ApproveTask_RunForNobody_IsNotAllowed()
    {
        // Arrange
        var (context, open, _) = await SeedAsync();
        var task = new ApproveRequestTask(context.Manager, context.Service, Access(), new HttpContextAccessor(), Evaluator(), RecipeRoundTrip.Localizer<ApproveRequestTask>()) { RequestId = open.ItemId };

        // Act
        var result = await task.ExecuteAsync(WorkflowContext(), new ActivityContext { Activity = task });

        // Assert
        Assert.Equal(["NotAllowed"], result.Outcomes);
        Assert.Equal(ApprovalStatus.Pending, open.Status);
    }

    [Fact]
    public async Task RejectTask_ByAnApprover_RejectsWithTheNote()
    {
        // Arrange
        var (context, _, groupA) = await SeedAsync();
        var task = new RejectRequestTask(context.Manager, context.Service, Access(), Accessor("pat"), Evaluator(), RecipeRoundTrip.Localizer<RejectRequestTask>()) { RequestId = groupA.ItemId, Note = "Totals are off" };

        // Act
        var result = await task.ExecuteAsync(WorkflowContext(), new ActivityContext { Activity = task });

        // Assert
        Assert.Equal(["Done"], result.Outcomes);
        Assert.Equal(ApprovalStatus.Rejected, groupA.Status);
        Assert.Equal("Totals are off", Assert.Single(groupA.Decisions).Note);
    }

    [Fact]
    public async Task GetRequestTask_ReadsTheStatus_FromTheCorrelationIdWhenNoIdIsGiven()
    {
        // Arrange
        var (context, open, _) = await SeedAsync();
        var task = new GetApprovalRequestTask(context.Manager, Evaluator(), RecipeRoundTrip.Localizer<GetApprovalRequestTask>());
        var workflow = WorkflowContext(open.ItemId);

        // Act
        var result = await task.ExecuteAsync(workflow, new ActivityContext { Activity = task });

        // Assert
        Assert.Equal(["Pending"], result.Outcomes);
        Assert.Equal("Pending", workflow.Output["ApprovalRequestStatus"]);
    }

    [Fact]
    public async Task RequestTask_AsksUnderThePolicy_AndWritesTheRequestId()
    {
        // Arrange
        var context = new ApprovalsTestContext();
        context.Policy();
        var task = new RequestApprovalTask(context.Service, Accessor("riley"), Evaluator(), RecipeRoundTrip.Localizer<RequestApprovalTask>())
        {
            Policy = "PayRunApproval",
            SubjectKind = PayRun,
            SubjectId = "run-9",
            Title = "Approve run 9",
        };
        var workflow = WorkflowContext();

        // Act
        var result = await task.ExecuteAsync(workflow, new ActivityContext { Activity = task });

        // Assert
        Assert.Equal(["Done"], result.Outcomes);
        var request = Assert.Single(context.Store.Entries);
        Assert.Equal(request.ItemId, workflow.Output["ApprovalRequestId"]);
        Assert.Equal("riley-id", request.RequesterUserId);
    }

    [Fact]
    public async Task RecipeStep_ImportsHistory_ThatIsNeverDecided_AndAPendingOneAsWithdrawn()
    {
        // Arrange
        var context = new ApprovalsTestContext();
        context.Policy();
        var step = new JsonObject
        {
            ["name"] = ApprovalsConstants.RequestsRecipeStepName,
            [ApprovalRequestsRecipeStep.RequestsProperty] = new JsonArray(
                new JsonObject { ["itemId"] = "legacy-approved", ["subjectKind"] = PayRun, ["subjectId"] = "old-1", ["status"] = "Approved", ["policyId"] = "policy-PayRunApproval" },
                new JsonObject { ["itemId"] = "legacy-pending", ["subjectKind"] = PayRun, ["subjectId"] = "old-2", ["status"] = "Pending", ["policyId"] = "policy-PayRunApproval" }),
        };

        // Act
        var errors = await RecipeRoundTrip.RunAsync(new ApprovalRequestsRecipeStep(context.Manager), step);
        var again = await RecipeRoundTrip.RunAsync(new ApprovalRequestsRecipeStep(context.Manager), step);
        var pending = context.Store.Entries.Single(entry => entry.ItemId == "legacy-pending");
        var decide = await context.Service.DecideAsync(pending, TestSecurity.User("pat"), ApprovalDecisionKind.Approve, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(errors);
        Assert.Empty(again);
        Assert.Equal(2, context.Store.Entries.Count);
        Assert.All(context.Store.Entries, entry => Assert.True(entry.IsHistorical));
        Assert.Equal(ApprovalStatus.Canceled, pending.Status);
        Assert.False(decide.Succeeded);
        Assert.Empty(context.Observer.Changes);
    }

    // A request about an open subject kind, and one about a pay run of employer group A, which Pat may see and Lee may
    // not. Riley asked for both.
    private static async Task<(ApprovalsTestContext Context, ApprovalRequest Open, ApprovalRequest GroupA)> SeedAsync()
    {
        var context = new ApprovalsTestContext();
        context.Policy();

        var open = (await context.Service.RequestAsync(new ApprovalSubmission { Policy = "PayRunApproval", SubjectKind = "Expense", SubjectId = "e-1", Title = "Expense" }, TestSecurity.User("riley"), TestContext.Current.CancellationToken)).Request;
        var groupA = await context.RequestAsync(subjectId: "run-a", scope: "groupA");

        return (context, open, groupA);
    }

    private static List<string> Ids(JsonObject step)
        => step[ApprovalRequestsRecipeStep.RequestsProperty]!.AsArray().Select(node => node!["itemId"]!.GetValue<string>()).ToList();

    private static SourceAccessService Access()
        => new([new FixedSourceAccess(PayRun, new Dictionary<string, SourceAccessScope> { ["pat"] = SourceAccessScope.Of(["groupA"]) })]);

    private static HttpContextAccessor Accessor(string userName)
        => new() { HttpContext = new DefaultHttpContext { User = TestSecurity.User(userName) } };

    private static ApprovalRequestsDeploymentSource ExportSource(ApprovalsTestContext context, IHttpContextAccessor accessor, RecordingNotifier notifier, params Permission[] grants)
        => new(context.Store, Access(), accessor, TestSecurity.Granting(grants), notifier, new EchoHtmlLocalizer<ApprovalRequestsDeploymentSource>());

    // A policy whose one rule names the team's manager, Morgan.
    private static ApprovalPolicy TimesheetPolicy(ApprovalsTestContext context)
    {
        context.Directory.People["Team"] = ["morgan"];

        var policy = new ApprovalPolicy
        {
            ItemId = "policy-Timesheet",
            Name = "TimesheetApproval",
            Title = "Time sheet approval",
            Approvers = [new ApproverRule { Kind = ApproverRuleKinds.Manager, Value = "Team" }],
        };

        context.Policies.Add(policy);

        return policy;
    }

    private static ApprovalRequestsController Controller(ApprovalsTestContext context, string userName, Permission[] grants = null)
    {
        var display = new Mock<global::OrchardCore.DisplayManagement.IDisplayManager<ApprovalRequest>>();
        var controller = new ApprovalRequestsController(
            context.Manager,
            context.Store,
            context.Service,
            context.Types,
            new ApprovalInbox(context.Store, context.Service, context.Access),
            Access(),
            TestSecurity.Granting(grants ?? [ApprovalsPermissions.ViewApprovalRequests, ApprovalsPermissions.ManageApprovalRequests]),
            display.Object,
            new Mock<global::OrchardCore.DisplayManagement.IDisplayManager<global::CrestApps.OrchardCore.Approvals.ViewModels.ApprovalDecideViewModel>>().Object,
            new Mock<global::OrchardCore.DisplayManagement.ModelBinding.IUpdateModelAccessor>().Object,
            new CrestApps.OrchardCore.Tests.Modules.Reports.Designer.PassThroughStringLocalizerFactory(),
            new RecordingNotifier(),
            new FixedOffsetLocalClock(() => context.Now, TimeSpan.FromHours(-5)),
            new EchoHtmlLocalizer<ApprovalRequestsController>(),
            RecipeRoundTrip.Localizer<ApprovalRequestsController>());

        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = TestSecurity.User(userName) } };

        return controller;
    }

    private static IWorkflowExpressionEvaluator Evaluator()
    {
        var evaluator = new Mock<IWorkflowExpressionEvaluator>();

        evaluator
            .Setup(service => service.EvaluateAsync(It.IsAny<WorkflowExpression<string>>(), It.IsAny<WorkflowExecutionContext>(), It.IsAny<TextEncoder>()))
            .Returns((WorkflowExpression<string> expression, WorkflowExecutionContext _, TextEncoder _) => Task.FromResult(expression.Expression));

        return evaluator.Object;
    }

    private static WorkflowExecutionContext WorkflowContext(string correlationId = null)
        => new(
            new WorkflowType(),
            new Workflow { CorrelationId = correlationId },
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            [],
            null,
            []);
}
