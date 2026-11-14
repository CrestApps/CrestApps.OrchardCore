using CrestApps.OrchardCore.Tickets.Controllers;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.ViewModels;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// How the ticket list turns its search and pickers into a query: with nothing chosen it shows the tickets still
/// being worked on; a saved view is the starting point and the pickers narrow it without changing the saved view; a
/// chosen status wins over the state picker; and the service-level picker asks for breached or soon due tickets.
/// </summary>
public sealed class TicketListQueryTests
{
    [Fact]
    public void WithNothingChosen_TheListShowsTheTicketsStillBeingWorkedOn()
    {
        // Act
        var query = TicketListQueries.Build(new TicketsListViewModel(), view: null);

        // Assert
        Assert.Equal([TicketStatusCategory.New, TicketStatusCategory.Open, TicketStatusCategory.Pending, TicketStatusCategory.OnHold], query.Categories);
        Assert.Equal(TicketSort.Updated, query.Sort);
        Assert.Null(query.Assignee);
    }

    [Fact]
    public void ASavedView_IsTheStartingPoint_AndThePickersNarrowItWithoutChangingTheView()
    {
        // Arrange
        var view = new TicketView
        {
            Name = "Unassigned",
            Query = new TicketQuery
            {
                Assignee = TicketAssignees.Unassigned,
                Categories = [TicketStatusCategory.New],
                Sort = TicketSort.Due,
            },
        };

        // Act
        var query = TicketListQueries.Build(new TicketsListViewModel { PriorityId = "urgent", Q = "printer" }, view);

        // Assert
        Assert.Equal(TicketAssignees.Unassigned, query.Assignee);
        Assert.Equal([TicketStatusCategory.New], query.Categories);
        Assert.Equal(TicketSort.Due, query.Sort);
        Assert.Equal(["urgent"], query.PriorityIds);
        Assert.Equal("printer", query.Search);
        Assert.Empty(view.Query.PriorityIds);
        Assert.Null(view.Query.Search);
    }

    [Fact]
    public void AChosenStatus_WinsOverTheState_AndEveryStateClearsTheCategories()
    {
        // Act
        var byStatus = TicketListQueries.Build(new TicketsListViewModel { StatusId = "solved", State = TicketListQueries.States.Open }, view: null);
        var every = TicketListQueries.Build(new TicketsListViewModel { State = TicketListQueries.States.All }, view: null);
        var closed = TicketListQueries.Build(new TicketsListViewModel { State = TicketListQueries.States.Closed }, view: null);

        // Assert
        Assert.Equal(["solved"], byStatus.StatusIds);
        Assert.Empty(byStatus.Categories);
        Assert.Empty(every.Categories);
        Assert.Equal([TicketStatusCategory.Closed], closed.Categories);
    }

    [Fact]
    public void TheServiceLevelPicker_AsksForBreachedOrSoonDueTickets()
    {
        // Act
        var breached = TicketListQueries.Build(new TicketsListViewModel { Due = TicketListQueries.DueFilters.Breached }, view: null);
        var soon = TicketListQueries.Build(new TicketsListViewModel { Due = TicketListQueries.DueFilters.Soon }, view: null);
        var unknown = TicketListQueries.Build(new TicketsListViewModel { Due = "whenever" }, view: null);

        // Assert
        Assert.Equal(TimeSpan.Zero, breached.DueWithin);
        Assert.Equal(TicketListQueries.SoonWindow, soon.DueWithin);
        Assert.Null(unknown.DueWithin);
    }

    [Fact]
    public void TheAssigneePickerAndSort_OverrideTheView()
    {
        // Arrange
        var view = new TicketView { Query = new TicketQuery { Assignee = TicketAssignees.Unassigned } };

        // Act
        var query = TicketListQueries.Build(new TicketsListViewModel { Assignee = TicketAssignees.Me, Sort = nameof(TicketSort.Priority), TeamId = "t1", DeskId = "d1", ContentType = "ItRequest" }, view);

        // Assert
        Assert.Equal(TicketAssignees.Me, query.Assignee);
        Assert.Equal(TicketSort.Priority, query.Sort);
        Assert.Equal(["t1"], query.TeamIds);
        Assert.Equal(["d1"], query.DeskIds);
        Assert.Equal(["ItRequest"], query.ContentTypes);
    }
}
