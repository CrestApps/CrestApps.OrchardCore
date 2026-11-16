using YesSql.Sql;

namespace CrestApps.OrchardCore.Tickets.Core.Indexes;

/// <summary>
/// Creates the index table of tickets.
/// </summary>
public static class TicketIndexSchema
{
    /// <summary>
    /// Creates the table and the indexes lists and views seek.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task that completes when the table exists.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.CreateMapIndexTableAsync<TicketIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentType", column => column.WithLength(255))
            .Column<string>("Number", column => column.WithLength(32))
            .Column<string>("Subject", column => column.Nullable().WithLength(TicketIndexProvider.SubjectLength))
            .Column<string>("DeskId", column => column.Nullable().WithLength(26))
            .Column<string>("StatusId", column => column.Nullable().WithLength(26))
            .Column<int>("StatusCategory")
            .Column<string>("PriorityId", column => column.Nullable().WithLength(26))
            .Column<int>("PriorityRank")
            .Column<string>("TeamId", column => column.Nullable().WithLength(26))
            .Column<string>("AssigneeId", column => column.Nullable().WithLength(26))
            .Column<string>("Requester", column => column.Nullable().WithLength(128))
            .Column<string>("RequesterEmail", column => column.Nullable().WithLength(TicketIndexProvider.EmailLength))
            .Column<string>("AccountId", column => column.Nullable().WithLength(26))
            .Column<string>("Channel", column => column.Nullable().WithLength(64))
            .Column<string>("MergedIntoId", column => column.Nullable().WithLength(26))
            .Column<DateTime>("CreatedUtc")
            .Column<DateTime>("ModifiedUtc")
            .Column<DateTime>("NextDueUtc", column => column.Nullable())
            .Column<DateTime>("DueSortUtc", column => column.Nullable())
            .Column<DateTime>("SolvedUtc", column => column.Nullable())
            .Column<DateTime>("ClosedUtc", column => column.Nullable())
            .Column<DateTime>("AssignedUtc", column => column.Nullable())
            .Column<DateTime>("FirstResponseUtc", column => column.Nullable())
            .Column<DateTime>("StatusChangedUtc", column => column.Nullable())
            .Column<DateTime>("LastCustomerActivityUtc", column => column.Nullable())
            .Column<DateTime>("LastAgentActivityUtc", column => column.Nullable())
            .Column<int>("ReplyCount", column => column.Nullable())
            .Column<int>("AgentReplyCount", column => column.Nullable())
            .Column<int>("ReopenCount", column => column.Nullable())
            .Column<int>("EscalationLevel", column => column.Nullable())
            .Column<int>("SatisfactionScore", column => column.Nullable())
            .Column<DateTime>("RatingRequestedUtc", column => column.Nullable())
            .Column<string>("CategoryId", column => column.Nullable().WithLength(26))
            .Column<string>("CategoryPath", column => column.Nullable().WithLength(TicketIndexProvider.CategoryPathLength))
            .Column<string>("SolvedById", column => column.Nullable().WithLength(26))
            .Column<string>("AgreementId", column => column.Nullable().WithLength(26)));

        await schemaBuilder.AlterIndexTableAsync<TicketIndex>(table =>
        {
            table.CreateIndex("IDX_TicketIndex_ContentItemId", "ContentItemId");
            table.CreateIndex("IDX_TicketIndex_Number", "Number");
            table.CreateIndex("IDX_TicketIndex_Desk", "DeskId", "StatusCategory", "ModifiedUtc", "DocumentId");
            table.CreateIndex("IDX_TicketIndex_Assignee", "AssigneeId", "StatusCategory", "ModifiedUtc", "DocumentId");
            table.CreateIndex("IDX_TicketIndex_Team", "TeamId", "StatusCategory", "DocumentId");
            table.CreateIndex("IDX_TicketIndex_Requester", "Requester", "CreatedUtc", "DocumentId");
            table.CreateIndex("IDX_TicketIndex_Account", "AccountId", "CreatedUtc", "DocumentId");
            table.CreateIndex("IDX_TicketIndex_Due", "StatusCategory", "NextDueUtc", "DocumentId");
            table.CreateIndex("IDX_TicketIndex_Solved", "StatusCategory", "SolvedUtc", "DocumentId");
            table.CreateIndex("IDX_TicketIndex_Rating", "DeskId", "RatingRequestedUtc", "SolvedUtc", "DocumentId");
        });

        await CreateAssignedIndexAsync(schemaBuilder);
        await CreateRequesterEmailIndexAsync(schemaBuilder);
        await CreateDueSortIndexAsync(schemaBuilder);
        await CreateTimeElapsedMarksAsync(schemaBuilder);
        await CreateEmailOutboxAsync(schemaBuilder);
        await CreateLinkIndexAsync(schemaBuilder);
        await CreateCategoryIndexAsync(schemaBuilder);
        await CreateProtectedItemIndexAsync(schemaBuilder);
    }

    /// <summary>
    /// Creates the index of tickets' protected items (ids only).
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateProtectedItemIndexAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.CreateMapIndexTableAsync<TicketProtectedItemIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("TicketId", column => column.WithLength(26)));

        await schemaBuilder.AlterIndexTableAsync<TicketProtectedItemIndex>(table => table
            .CreateIndex("IDX_TicketProtectedItem_Ticket", "TicketId", "ItemId"));
    }

    /// <summary>
    /// Creates the index the category filter reads.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static Task CreateCategoryIndexAsync(ISchemaBuilder schemaBuilder)
        => schemaBuilder.AlterIndexTableAsync<TicketIndex>(table => table
            .CreateIndex("IDX_TicketIndex_Category", "CategoryId", "DocumentId"));

    /// <summary>
    /// Creates the table of ticket-to-ticket links.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateLinkIndexAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.CreateMapIndexTableAsync<TicketLinkIndex>(table => table
            .Column<string>("TicketId", column => column.WithLength(26))
            .Column<string>("LinkedTicketId", column => column.WithLength(26))
            .Column<string>("Kind", column => column.WithLength(50)));

        await schemaBuilder.AlterIndexTableAsync<TicketLinkIndex>(table =>
            table.CreateIndex("IDX_TicketLinkIndex_Linked", "LinkedTicketId", "Kind", "DocumentId"));
    }

    /// <summary>
    /// Creates the table of the requester e-mails' outbox.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateEmailOutboxAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.CreateMapIndexTableAsync<TicketEmailOutboxIndex>(table => table
            .Column<string>("TicketId", column => column.WithLength(26))
            .Column<int>("Status")
            .Column<DateTime>("NextAttemptUtc", column => column.Nullable()));

        await schemaBuilder.AlterIndexTableAsync<TicketEmailOutboxIndex>(table =>
        {
            table.CreateIndex("IDX_TicketEmailOutbox_Due", "Status", "NextAttemptUtc", "DocumentId");
            table.CreateIndex("IDX_TicketEmailOutbox_Ticket", "TicketId");
        });
    }

    /// <summary>
    /// Creates the table of the time-elapsed markers: which workflow start has fired for which ticket and moment.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateTimeElapsedMarksAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.CreateMapIndexTableAsync<TicketTimeElapsedMarkIndex>(table => table
            .Column<string>("StartKey", column => column.WithLength(64))
            .Column<string>("TicketId", column => column.WithLength(26))
            .Column<DateTime>("Moment")
            .Column<DateTime>("FiredUtc"));

        await schemaBuilder.AlterIndexTableAsync<TicketTimeElapsedMarkIndex>(table =>
        {
            table.CreateIndex("IDX_TicketTimeElapsedMark_Start", "StartKey", "TicketId");
            table.CreateIndex("IDX_TicketTimeElapsedMark_Fired", "FiredUtc", "DocumentId");
        });
    }

    /// <summary>
    /// Creates the index the list sorts by due time on.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static Task CreateDueSortIndexAsync(ISchemaBuilder schemaBuilder)
        => schemaBuilder.AlterIndexTableAsync<TicketIndex>(table => table
            .CreateIndex("IDX_TicketIndex_DueSort", "DueSortUtc", "DocumentId"));

    /// <summary>
    /// Creates the index that finds a person's tickets by their e-mail.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static Task CreateRequesterEmailIndexAsync(ISchemaBuilder schemaBuilder)
        => schemaBuilder.AlterIndexTableAsync<TicketIndex>(table => table
            .CreateIndex("IDX_TicketIndex_RequesterEmail", "RequesterEmail", "DocumentId"));

    /// <summary>
    /// Creates the index team assignment reads to take turns: the latest assignment of each assignee.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task that completes when the index exists.</returns>
    public static Task CreateAssignedIndexAsync(ISchemaBuilder schemaBuilder)
        => schemaBuilder.AlterIndexTableAsync<TicketIndex>(table => table
            .CreateIndex("IDX_TicketIndex_Assigned", "AssigneeId", "AssignedUtc", "DocumentId"));

    /// <summary>
    /// Creates the table of the daily backlog snapshots.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateBacklogIndexAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.CreateMapIndexTableAsync<TicketBacklogIndex>(table => table
            .Column<string>("Day", column => column.WithLength(10))
            .Column<string>("DeskId", column => column.Nullable().WithLength(26))
            .Column<DateTime>("TakenUtc"));

        await schemaBuilder.AlterIndexTableAsync<TicketBacklogIndex>(table =>
        {
            table.CreateIndex("IDX_TicketBacklogIndex_Day", "Day", "DeskId");
            table.CreateIndex("IDX_TicketBacklogIndex_Taken", "TakenUtc", "DocumentId");
        });
    }
}
