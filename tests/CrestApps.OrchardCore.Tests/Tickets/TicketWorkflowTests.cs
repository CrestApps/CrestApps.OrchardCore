using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tickets.Workflows;
using CrestApps.OrchardCore.Timeline.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// Ticket workflows: what the ticket service tells its observers starts the matching events (a move into Solved also
/// starts Ticket Solved, a move back out of it Ticket Reopened; a note starts Note Added, not Agent Replied); events can
/// be narrowed to desks, changed fields, status categories and an action's name; a chain of workflow-caused changes
/// stops starting workflows at a fixed depth; a task's change counts toward that depth; and the service-level check
/// reads the active timers.
/// </summary>
public sealed class TicketWorkflowTests
{
    private readonly List<(string Name, IDictionary<string, object> Input)> _triggered = [];

    [Fact]
    public async Task WhatTheServiceSays_StartsTheMatchingEvents()
    {
        // Arrange
        var observer = Observer();
        var ticket = Ticket();
        var open = new TicketStatus { ItemId = "open", Category = TicketStatusCategory.Open };
        var solved = new TicketStatus { ItemId = "solved", Category = TicketStatusCategory.Solved };

        // Act
        await observer.CreatedAsync(ticket, TestContext.Current.CancellationToken);
        await observer.StatusChangedAsync(ticket, open, solved, TestContext.Current.CancellationToken);
        await observer.StatusChangedAsync(ticket, solved, open, TestContext.Current.CancellationToken);
        await observer.RepliedAsync(ticket, new TimelineEntry { Visibility = TimelineVisibility.Internal, Body = "Note" }, fromRequester: false, TestContext.Current.CancellationToken);
        await observer.RepliedAsync(ticket, new TimelineEntry { Visibility = TimelineVisibility.Public, Body = "Again" }, fromRequester: true, TestContext.Current.CancellationToken);
        await observer.ServiceLevelAsync(ticket, ServiceLevelMetrics.FirstResponse, breached: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                TicketsConstants.Events.Created,
                TicketsConstants.Events.StatusChanged, TicketsConstants.Events.Solved,
                TicketsConstants.Events.StatusChanged, TicketsConstants.Events.Reopened,
                TicketsConstants.Events.NoteAdded,
                TicketsConstants.Events.CustomerReplied,
                TicketsConstants.Events.ServiceLevelBreached,
            ],
            _triggered.Select(item => item.Name));
        Assert.Equal("t1", _triggered[0].Input[TicketWorkflowInput.TicketId]);
        Assert.Equal("support", _triggered[0].Input[TicketWorkflowInput.DeskId]);
        Assert.Equal(ServiceLevelMetrics.FirstResponse, _triggered[^1].Input[TicketWorkflowInput.Metric]);
    }

    [Fact]
    public async Task AChainOfWorkflowCausedChanges_StopsStartingWorkflowsAtTheMaximumDepth()
    {
        // Arrange
        var observer = Observer();
        var scopes = Enumerable.Range(0, TicketWorkflowDepth.MaxDepth).Select(_ => TicketWorkflowDepth.Enter()).ToList();

        try
        {
            // Act
            await observer.CreatedAsync(Ticket(), TestContext.Current.CancellationToken);
        }
        finally
        {
            scopes.ForEach(scope => scope.Dispose());
        }

        await observer.CreatedAsync(Ticket(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(_triggered);
        Assert.Equal(0, TicketWorkflowDepth.Current);
    }

    [Fact]
    public void Events_CanBeNarrowedToDesksFieldsCategoriesAndAnAction()
    {
        // Arrange
        var updated = new TicketUpdatedEvent(RecipeRoundTrip.Localizer<TicketUpdatedEvent>()) { DeskIds = ["support"], ChangedFields = ["Priority"] };
        var statusChanged = new TicketStatusChangedEvent(RecipeRoundTrip.Localizer<TicketStatusChangedEvent>()) { Categories = ["Solved"] };
        var action = new TicketActionRequestedEvent(RecipeRoundTrip.Localizer<TicketActionRequestedEvent>()) { ActionName = "Escalate to tier 2" };

        // Act and assert
        Assert.True(updated.CanExecute(Context(new() { [TicketWorkflowInput.DeskId] = "support", [TicketWorkflowInput.ChangedFields] = new[] { "Priority", "Team" } }), null));
        Assert.False(updated.CanExecute(Context(new() { [TicketWorkflowInput.DeskId] = "hr", [TicketWorkflowInput.ChangedFields] = new[] { "Priority" } }), null));
        Assert.False(updated.CanExecute(Context(new() { [TicketWorkflowInput.DeskId] = "support", [TicketWorkflowInput.ChangedFields] = new[] { "Team" } }), null));
        Assert.True(statusChanged.CanExecute(Context(new() { [TicketWorkflowInput.StatusCategory] = "Solved" }), null));
        Assert.False(statusChanged.CanExecute(Context(new() { [TicketWorkflowInput.StatusCategory] = "Open" }), null));
        Assert.True(action.CanExecute(Context(new() { [TicketWorkflowInput.ActionName] = "escalate to tier 2" }), null));
        Assert.False(action.CanExecute(Context(new() { [TicketWorkflowInput.ActionName] = "Close" }), null));
    }

    [Fact]
    public async Task ATasksChange_IsAWorkflows_AndCountsTowardTheDepth()
    {
        // Arrange
        var replies = new List<(TicketReply Reply, int Depth)>();
        var tickets = new Mock<ITicketService>();
        tickets.Setup(service => service.GetAsync("t1")).ReturnsAsync(Ticket());
        tickets.Setup(service => service.ReplyAsync("t1", It.IsAny<TicketReply>(), It.IsAny<CancellationToken>()))
            .Callback((string _, TicketReply reply, CancellationToken _) => replies.Add((reply, TicketWorkflowDepth.Current)))
            .ReturnsAsync(new TicketResult());

        var evaluator = new Mock<IWorkflowExpressionEvaluator>();
        evaluator.Setup(value => value.EvaluateAsync(It.IsAny<WorkflowExpression<string>>(), It.IsAny<WorkflowExecutionContext>(), It.IsAny<System.Text.Encodings.Web.TextEncoder>()))
            .ReturnsAsync((WorkflowExpression<string> expression, WorkflowExecutionContext _, System.Text.Encodings.Web.TextEncoder _) => expression.Expression == TicketTaskActivity.DefaultTicketIdExpression ? "t1" : expression.Expression);

        var services = new ServiceCollection().AddSingleton(tickets.Object).BuildServiceProvider();
        var task = new AddTicketNoteTask(services, evaluator.Object, RecipeRoundTrip.Localizer<AddTicketNoteTask>());
        task.SetValue("Body", "Checked the logs.");

        // Act
        var result = await task.ExecuteAsync(Context([]), null);

        // Assert
        Assert.Equal(["Done"], result.Outcomes);
        var (reply, depth) = Assert.Single(replies);
        Assert.True(reply.IsInternal);
        Assert.Equal("Checked the logs.", reply.Body);
        Assert.Equal(1, depth);
        Assert.Equal(0, TicketWorkflowDepth.Current);
    }

    [Fact]
    public void TheServiceLevelCheck_ReadsTheActiveTimers()
    {
        // Act and assert
        Assert.Equal("None", CheckTicketServiceLevelTask.StandingOf([]));
        Assert.Equal("Breached", CheckTicketServiceLevelTask.StandingOf([new ServiceLevelTimer { BreachedUtc = DateTime.UtcNow }, new ServiceLevelTimer()]));
        Assert.Equal("Warning", CheckTicketServiceLevelTask.StandingOf([new ServiceLevelTimer { WarnedUtc = DateTime.UtcNow }]));
        Assert.Equal("Paused", CheckTicketServiceLevelTask.StandingOf([new ServiceLevelTimer { State = ServiceLevelTimerState.Paused }]));
        Assert.Equal("OnTrack", CheckTicketServiceLevelTask.StandingOf([new ServiceLevelTimer { State = ServiceLevelTimerState.Running }]));
    }

    private TicketWorkflowObserver Observer()
    {
        var workflowManager = new Mock<IWorkflowManager>();
        workflowManager.Setup(manager => manager.TriggerEventAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .Callback((string name, IDictionary<string, object> input, string _, bool _, bool _) => _triggered.Add((name, input)))
            .Returns(Task.FromResult<IEnumerable<WorkflowExecutionContext>>([]));

        return new TicketWorkflowObserver(workflowManager.Object, NullLogger<TicketWorkflowObserver>.Instance);
    }

    private static ContentItem Ticket()
    {
        var ticket = new ContentItem { ContentItemId = "t1", ContentType = TicketsConstants.DefaultContentType, DisplayText = "Printer jams" };
        ticket.Apply(new TicketPart { Number = "T-1", DeskId = "support", StatusId = "open", StatusCategory = TicketStatusCategory.Open });

        return ticket;
    }

    private static WorkflowExecutionContext Context(Dictionary<string, object> input)
        => new(
            new WorkflowType { WorkflowTypeId = "type-1" },
            new Workflow { WorkflowId = "w1" },
            input,
            new Dictionary<string, object>(),
            new Dictionary<string, object>(),
            [],
            null,
            []);
}
