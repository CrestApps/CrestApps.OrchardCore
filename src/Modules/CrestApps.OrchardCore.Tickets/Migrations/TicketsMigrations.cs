using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Title.Models;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tickets.Migrations;

/// <summary>
/// Creates the ticket part, the default ticket type and the ticket index, and seeds the statuses, priorities, a desk and
/// the saved views and the board a help desk starts with.
/// </summary>
internal sealed class TicketsMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ICatalogManager<TicketStatus> _statuses;
    private readonly ICatalogManager<TicketPriority> _priorities;
    private readonly ICatalogManager<TicketDesk> _desks;
    private readonly ICatalogManager<TicketView> _views;
    private readonly ICatalogManager<TicketBoard> _boards;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketsMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="statuses">The statuses.</param>
    /// <param name="priorities">The priorities.</param>
    /// <param name="desks">The desks.</param>
    /// <param name="views">The saved views.</param>
    /// <param name="boards">The boards.</param>
    public TicketsMigrations(
        IContentDefinitionManager contentDefinitionManager,
        ICatalogManager<TicketStatus> statuses,
        ICatalogManager<TicketPriority> priorities,
        ICatalogManager<TicketDesk> desks,
        ICatalogManager<TicketView> views,
        ICatalogManager<TicketBoard> boards)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _statuses = statuses;
        _priorities = priorities;
        _desks = desks;
        _views = views;
        _boards = boards;
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
                .Listable(false)
                .Creatable(false)
                .WithPart<TitlePart>(part => part
                    .WithDisplayName("Subject")
                    .WithPosition("1")
                    .WithSettings(new TitlePartSettings { Options = TitlePartOptions.EditableRequired }))
                .WithPart(TicketsConstants.PartName, part => part.WithPosition("2")));
        }

        await TicketIndexSchema.CreateAsync(SchemaBuilder);
        await TicketIndexSchema.CreateBacklogIndexAsync(SchemaBuilder);
        await SeedAsync();

        return 12;
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

    /// <summary>
    /// Adds the daily backlog snapshots.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await TicketIndexSchema.CreateBacklogIndexAsync(SchemaBuilder);

        return 3;
    }

    /// <summary>
    /// Adds the requester's e-mail to the ticket index, so a person's tickets are found by their e-mail.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> UpdateFrom3Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<TicketIndex>(table => table
            .AddColumn<string>("RequesterEmail", column => column.Nullable().WithLength(TicketIndexProvider.EmailLength)));
        await TicketIndexSchema.CreateRequesterEmailIndexAsync(SchemaBuilder);

        return 4;
    }

    /// <summary>
    /// Adds the due time the list sorts on, filled in for the tickets already indexed.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> UpdateFrom4Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<TicketIndex>(table => table
            .AddColumn<DateTime>("DueSortUtc", column => column.Nullable()));
        await TicketIndexSchema.CreateDueSortIndexAsync(SchemaBuilder);

        // The rows indexed before the column existed get their value once the migration's transaction is committed.
        ShellScope.AddDeferredTask(scope => TicketIndexBackfill.FillDueSortAsync(scope.ServiceProvider));

        return 5;
    }

    /// <summary>
    /// Takes ticket types out of Orchard Core's generic content list and new-item menu: tickets are listed, opened and
    /// changed on the ticket pages, under the ticket permissions.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> UpdateFrom5Async()
    {
        foreach (var type in await _contentDefinitionManager.ListTypeDefinitionsAsync())
        {
            if (type.Parts.Any(part => part.PartDefinition.Name == TicketsConstants.PartName))
            {
                await _contentDefinitionManager.AlterTypeDefinitionAsync(type.Name, builder => builder.Listable(false).Creatable(false));
            }
        }

        return 6;
    }

    /// <summary>
    /// Keys the targets of ticket policies, and the priority of running ticket timers, by the priority's id instead of
    /// its name, so renaming a priority no longer detaches its targets.
    /// </summary>
    /// <returns>The version.</returns>
    public static int UpdateFrom6()
    {
        ShellScope.AddDeferredTask(scope => TicketPriorityKeys.MoveToIdsAsync(scope.ServiceProvider));

        return 7;
    }

    /// <summary>
    /// Adds the ticket times and counts the time-elapsed workflows and the reports read from the index, and the
    /// time-elapsed markers; the tickets already indexed are saved again by a resumable step to fill them.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> UpdateFrom7Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<TicketIndex>(table =>
        {
            table.AddColumn<DateTime>("FirstResponseUtc", column => column.Nullable());
            table.AddColumn<DateTime>("StatusChangedUtc", column => column.Nullable());
            table.AddColumn<DateTime>("LastCustomerActivityUtc", column => column.Nullable());
            table.AddColumn<DateTime>("LastAgentActivityUtc", column => column.Nullable());
            table.AddColumn<int>("ReplyCount", column => column.Nullable());
            table.AddColumn<int>("AgentReplyCount", column => column.Nullable());
            table.AddColumn<int>("ReopenCount", column => column.Nullable());
            table.AddColumn<int>("EscalationLevel", column => column.Nullable());
        });
        await TicketIndexSchema.CreateTimeElapsedMarksAsync(SchemaBuilder);

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<ResumableSteps>().RequestAsync(TicketReindexStep.StepName));

        return 8;
    }

    /// <summary>
    /// Adds the outbox of the requester e-mails.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> UpdateFrom8Async()
    {
        await TicketIndexSchema.CreateEmailOutboxAsync(SchemaBuilder);

        return 9;
    }

    /// <summary>
    /// Adds the requester's rating and when they were asked for it to the ticket index.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> UpdateFrom9Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<TicketIndex>(table =>
        {
            table.AddColumn<int>("SatisfactionScore", column => column.Nullable());
            table.AddColumn<DateTime>("RatingRequestedUtc", column => column.Nullable());
        });
        await SchemaBuilder.AlterIndexTableAsync<TicketIndex>(table => table.CreateIndex("IDX_TicketIndex_Rating", "DeskId", "RatingRequestedUtc", "SolvedUtc", "DocumentId"));

        return 10;
    }

    /// <summary>
    /// Adds the index of ticket-to-ticket links and fills it by saving every ticket again in the background.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> UpdateFrom10Async()
    {
        await TicketIndexSchema.CreateLinkIndexAsync(SchemaBuilder);

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<ResumableSteps>().RequestAsync(TicketReindexStep.StepName));

        return 11;
    }

    /// <summary>
    /// Adds a board shared with everybody, with a column per status category, when the site has no board yet.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> UpdateFrom11Async()
    {
        await SeedBoardAsync();

        return 12;
    }

    private async Task SeedAsync()
    {
        // The catalog reads its cached copy, which doesn't show records made earlier in this scope, so the board is
        // made from the statuses created here.
        var created = new List<TicketStatus>();

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
                created.Add(status);
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

        await SeedBoardAsync(created);
    }

    private async Task SeedBoardAsync(IReadOnlyList<TicketStatus> created = null)
    {
        if ((await _boards.GetAllAsync()).Any())
        {
            return;
        }

        IEnumerable<TicketStatus> statuses = created is { Count: > 0 } ? created : await _statuses.GetAllAsync();
        var columns = TicketBoardDefaults.Columns(statuses, TicketBoardDefaults.EnglishName);

        if (columns.Count == 0)
        {
            return;
        }

        var board = await _boards.NewAsync();
        board.Name = "Service desk";
        board.Description = "Tickets by status. Drag a card to another column to change its status.";
        board.Columns = columns;
        await _boards.CreateAsync(board);
    }

}
