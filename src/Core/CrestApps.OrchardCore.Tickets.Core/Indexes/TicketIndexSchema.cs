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
            .Column<DateTime>("SolvedUtc", column => column.Nullable())
            .Column<DateTime>("ClosedUtc", column => column.Nullable())
            .Column<DateTime>("AssignedUtc", column => column.Nullable()));

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
        });

        await CreateAssignedIndexAsync(schemaBuilder);
        await CreateRequesterEmailIndexAsync(schemaBuilder);
    }

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
