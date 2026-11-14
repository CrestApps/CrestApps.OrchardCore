using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Title.Models;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tickets.Migrations;

/// <summary>
/// Creates the ticket part, the default ticket type and the ticket index, and seeds the statuses, priorities, a desk and
/// the saved views a help desk starts with.
/// </summary>
internal sealed class TicketsMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ICatalogManager<TicketStatus> _statuses;
    private readonly ICatalogManager<TicketPriority> _priorities;
    private readonly ICatalogManager<TicketDesk> _desks;
    private readonly ICatalogManager<TicketView> _views;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketsMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="statuses">The statuses.</param>
    /// <param name="priorities">The priorities.</param>
    /// <param name="desks">The desks.</param>
    /// <param name="views">The saved views.</param>
    public TicketsMigrations(
        IContentDefinitionManager contentDefinitionManager,
        ICatalogManager<TicketStatus> statuses,
        ICatalogManager<TicketPriority> priorities,
        ICatalogManager<TicketDesk> desks,
        ICatalogManager<TicketView> views)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _statuses = statuses;
        _priorities = priorities;
        _desks = desks;
        _views = views;
    }

    /// <summary>
    /// Creates everything.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(TicketsConstants.PartName, part => part
            .Attachable()
            .WithDisplayName("Ticket")
            .WithDescription("Makes a content type a ticket type: its items are tickets, filed under a desk, with a status, a priority, a requester and a timeline."));

        if (await _contentDefinitionManager.GetTypeDefinitionAsync(TicketsConstants.DefaultContentType) is null)
        {
            await _contentDefinitionManager.AlterTypeDefinitionAsync(TicketsConstants.DefaultContentType, type => type
                .WithDisplayName("Ticket")
                .Stereotype(TicketsConstants.Stereotype)
                .WithPart<TitlePart>(part => part
                    .WithDisplayName("Subject")
                    .WithPosition("1")
                    .WithSettings(new TitlePartSettings { Options = TitlePartOptions.EditableRequired }))
                .WithPart(TicketsConstants.PartName, part => part.WithPosition("2")));
        }

        await TicketIndexSchema.CreateAsync(SchemaBuilder);
        await SeedAsync();

        return 2;
    }

    /// <summary>
    /// Adds when each ticket was last given to its assignee, so team assignment can take turns.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<TicketIndex>(table => table
            .AddColumn<DateTime>("AssignedUtc", column => column.Nullable()));
        await TicketIndexSchema.CreateAssignedIndexAsync(SchemaBuilder);

        return 2;
    }

    private async Task SeedAsync()
    {
        if (!(await _statuses.GetAllAsync()).Any())
        {
            (string Name, TicketStatusCategory Category, string CustomerLabel, bool Pauses, string Color, int Order)[] statuses =
            [
                ("New", TicketStatusCategory.New, "Received", false, "#0d6efd", 10),
                ("Open", TicketStatusCategory.Open, "In progress", false, "#dc3545", 20),
                ("Waiting for the requester", TicketStatusCategory.Pending, "Waiting for your reply", true, "#6f42c1", 30),
                ("On hold", TicketStatusCategory.OnHold, "In progress", false, "#6c757d", 40),
                ("Solved", TicketStatusCategory.Solved, "Solved", false, "#198754", 50),
                ("Closed", TicketStatusCategory.Closed, "Closed", false, "#495057", 60),
            ];

            foreach (var (name, category, label, pauses, color, order) in statuses)
            {
                var status = await _statuses.NewAsync();
                status.Name = name;
                status.Category = category;
                status.CustomerLabel = label;
                status.PausesTimers = pauses;
                status.Color = color;
                status.Order = order;
                await _statuses.CreateAsync(status);
            }
        }

        if (!(await _priorities.GetAllAsync()).Any())
        {
            (string Name, int Rank, string Color)[] priorities =
            [
                ("Low", 1, "#6c757d"),
                ("Normal", 2, "#0d6efd"),
                ("High", 3, "#fd7e14"),
                ("Urgent", 4, "#dc3545"),
            ];

            foreach (var (name, rank, color) in priorities)
            {
                var priority = await _priorities.NewAsync();
                priority.Name = name;
                priority.Rank = rank;
                priority.Color = color;
                await _priorities.CreateAsync(priority);
            }
        }

        if (!(await _desks.GetAllAsync()).Any())
        {
            var desk = await _desks.NewAsync();
            desk.Name = "Support";
            desk.Description = "Questions and problems from customers.";
            desk.Audience = TicketDeskAudience.Customers;
            await _desks.CreateAsync(desk);
        }

        if (!(await _views.GetAllAsync()).Any())
        {
            TicketStatusCategory[] unsolved = [TicketStatusCategory.New, TicketStatusCategory.Open, TicketStatusCategory.Pending, TicketStatusCategory.OnHold];

            (string Name, TicketQuery Query)[] views =
            [
                ("My open tickets", new TicketQuery { Assignee = TicketAssignees.Me, Categories = [.. unsolved] }),
                ("Unassigned", new TicketQuery { Assignee = TicketAssignees.Unassigned, Categories = [.. unsolved] }),
                ("Breaching soon", new TicketQuery { Categories = [.. unsolved], DueWithin = TimeSpan.FromHours(4), Sort = TicketSort.Due }),
                ("Recently solved", new TicketQuery { Categories = [TicketStatusCategory.Solved] }),
            ];

            var order = 0;

            foreach (var (name, query) in views)
            {
                var view = await _views.NewAsync();
                view.Name = name;
                view.Query = query;
                view.Order = order += 10;
                await _views.CreateAsync(view);
            }
        }
    }
}
