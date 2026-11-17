using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Title.Models;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tickets.Migrations;

/// <summary>
/// Creates the ticket part, the default ticket type and the ticket index, and seeds the statuses, priorities, a desk and
/// the saved views and the board a help desk starts with.
/// </summary>
internal sealed class TicketsMigrations : DataMigration
{
    // The help desk starter recipe's IT request type and its Root cause field.
    private const string StarterITRequestPart = "ITRequest";
    private const string StarterRootCauseField = "RootCause";

    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ICatalogManager<TicketStatus> _statuses;
    private readonly ICatalogManager<TicketPriority> _priorities;
    private readonly ICatalogManager<TicketDesk> _desks;
    private readonly ICatalogManager<TicketView> _views;
    private readonly ICatalogManager<TicketBoard> _boards;
    private readonly ResumableSteps _steps;
    private readonly IStore _store;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketsMigrations"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="statuses">The statuses.</param>
    /// <param name="priorities">The priorities.</param>
    /// <param name="desks">The desks.</param>
    /// <param name="views">The saved views.</param>
    /// <param name="boards">The boards.</param>
    /// <param name="steps">The resumable steps, which a request adds to in the migration's own transaction.</param>
    /// <param name="store">The store, whose table naming and serializer the move to the service desk's collection uses.</param>
    /// <param name="session">The migration's session, which saves the moved records.</param>
    public TicketsMigrations(
        IContentDefinitionManager contentDefinitionManager,
        ICatalogManager<TicketStatus> statuses,
        ICatalogManager<TicketPriority> priorities,
        ICatalogManager<TicketDesk> desks,
        ICatalogManager<TicketView> views,
        ICatalogManager<TicketBoard> boards,
        ResumableSteps steps,
        IStore store,
        ISession session)
    {
        _steps = steps;
        _store = store;
        _session = session;
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
        await TicketIndexSchema.CreateServiceDeskAsync(SchemaBuilder);
        await DefineCategoriesAsync();
        await SeedAsync();

        return 18;
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
        // In the default collection, as then; version 17 moves it to the service desk's.
        await TicketIndexSchema.CreateBacklogIndexAsync(SchemaBuilder, collection: null);

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
        await TicketIndexSchema.CreateTimeElapsedMarksAsync(SchemaBuilder, collection: null);

        await _steps.RequestAsync(TicketReindexStep.StepName);

        return 8;
    }

    /// <summary>
    /// Adds the outbox of the requester e-mails.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> UpdateFrom8Async()
    {
        await TicketIndexSchema.CreateEmailOutboxAsync(SchemaBuilder, collection: null);

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

        await _steps.RequestAsync(TicketReindexStep.StepName);

        return 11;
    }

    /// <summary>
    /// Was where a board shared with everybody was added; version 17 adds it, in the service desk's collection, when the
    /// site has no board yet, since the boards weren't there before.
    /// </summary>
    /// <returns>The version.</returns>
    public static int UpdateFrom11()
    {
        return 12;
    }

    /// <summary>
    /// Adds the category to the ticket index, and the ticket category part and term type. Tickets saved before have no
    /// category, so nothing is reindexed.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> UpdateFrom12Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<TicketIndex>(table =>
        {
            table.AddColumn<string>("CategoryId", column => column.Nullable().WithLength(26));
            table.AddColumn<string>("CategoryPath", column => column.Nullable().WithLength(TicketIndexProvider.CategoryPathLength));
        });
        await TicketIndexSchema.CreateCategoryIndexAsync(SchemaBuilder);
        await DefineCategoriesAsync();

        return 13;
    }

    /// <summary>
    /// Adds who solved a ticket to the ticket index.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> UpdateFrom13Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<TicketIndex>(table => table.AddColumn<string>("SolvedById", column => column.Nullable().WithLength(26)));

        return 14;
    }

    /// <summary>
    /// Adds the agreement a ticket works under to the ticket index.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> UpdateFrom14Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<TicketIndex>(table =>
        {
            table.AddColumn<string>("AgreementId", column => column.Nullable().WithLength(26));
            table.CreateIndex("IDX_TicketIndex_Agreement", "AgreementId", "DocumentId");
        });

        return 15;
    }

    /// <summary>
    /// Adds the index of tickets' protected items.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> UpdateFrom15Async()
    {
        await TicketIndexSchema.CreateProtectedItemIndexAsync(SchemaBuilder, collection: null);

        return 16;
    }

    /// <summary>
    /// Moves the service desk's own records to its collection (<see cref="TicketsConstants.CollectionName"/>): the desks,
    /// statuses, priorities, saved views and boards, each a document of its own instead of one dictionary per kind, and
    /// the protected information, the requester e-mail outbox, the backlog snapshots and the time-elapsed markers. Their
    /// old index tables in the default collection go first, then the new ones are created and the records moved. A site
    /// without a board then gets the shared one.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> UpdateFrom16Async()
    {
        // The default collection's index tables carry the type's name; the collection's own carry its prefix.
        await SchemaBuilder.DropTableAsync("TicketProtectedItemIndex");
        await SchemaBuilder.DropTableAsync("TicketEmailOutboxIndex");
        await SchemaBuilder.DropTableAsync("TicketBacklogIndex");
        await SchemaBuilder.DropTableAsync("TicketTimeElapsedMarkIndex");
        await TicketIndexSchema.CreateServiceDeskAsync(SchemaBuilder);
        await TicketServiceDeskMove.MoveAsync(SchemaBuilder, _store, _session);
        await SeedBoardAsync();

        return 17;
    }

    /// <summary>
    /// Gives the help desk starter's Root cause field (on the IT request type) a multi-line editor, as the recipe now does,
    /// where the site ran the recipe and the field still has the default single-line one.
    /// </summary>
    /// <returns>The version.</returns>
    public async Task<int> UpdateFrom17Async()
    {
        var part = await _contentDefinitionManager.GetPartDefinitionAsync(StarterITRequestPart);
        var field = part?.Fields.FirstOrDefault(value => value.Name == StarterRootCauseField && value.FieldDefinition.Name == "TextField");

        if (field is not null && string.IsNullOrEmpty(field.Editor()))
        {
            await _contentDefinitionManager.AlterPartDefinitionAsync(StarterITRequestPart, builder => builder
                .WithField(StarterRootCauseField, fieldBuilder => fieldBuilder.WithEditor("TextArea")));
        }

        return 18;
    }

    // The part a ticket category's term carries, and a term type with it for taxonomies of ticket categories.
    private async Task DefineCategoriesAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(TicketsConstants.CategoryPartName, part => part
            .Attachable()
            .WithDisplayName("Ticket category")
            .WithDescription("Makes a taxonomy term a ticket category: the desk, team, priority and service level its tickets start with, and whether customers can choose it."));

        if (await _contentDefinitionManager.GetTypeDefinitionAsync(TicketsConstants.CategoryTermType) is null)
        {
            await _contentDefinitionManager.AlterTypeDefinitionAsync(TicketsConstants.CategoryTermType, type => type
                .WithDisplayName("Ticket category")
                .Listable(false)
                .Creatable(false)
                .WithPart<TitlePart>(part => part
                    .WithPosition("1")
                    .WithSettings(new TitlePartSettings { Options = TitlePartOptions.EditableRequired }))
                .WithPart(TicketsConstants.CategoryPartName, part => part.WithPosition("2")));
        }
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
