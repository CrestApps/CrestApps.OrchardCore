using Dapper;
using YesSql.Sql;
using YesSql.Sql.Schema;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Creates the index tables of Surveys.
/// </summary>
public static class SurveysIndexSchema
{
    /// <summary>
    /// Creates the index tables of surveys, versions, banks, collectors, responses, participation, invitations and
    /// accommodations.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = SurveysConstants.CollectionName;
        const string responses = SurveysConstants.ResponsesCollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<SurveyIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(100))
            .Column<string>("Title", column => column.Nullable().WithLength(255))
            .Column<string>("Kind", column => column.Nullable().WithLength(50))
            .Column<string>("Status", column => column.Nullable().WithLength(20))
            .Column<string>("OwnerRef", column => column.Nullable().WithLength(255))
            .Column<int>("PublishedVersionNumber")
            .Column<DateTime?>("ModifiedUtc", column => column.Nullable())
            .Column<string>("OwnerId", column => column.Nullable().WithLength(26))
            .Column<string>("Anonymity", column => column.Nullable().WithLength(20))
            .Column<int?>("MinimumGroupSize", column => column.Nullable())
            .Column<int?>("RetentionDays", column => column.Nullable())
            .Column<DateTime?>("ClosesUtc", column => column.Nullable())
            .Column<int?>("TotalLimit", column => column.Nullable())
            .Column<DateTime?>("DeletingUtc", column => column.Nullable()),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<SurveyIndex>(table =>
        {
            table.CreateIndex("IDX_SurveyIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_SurveyIndex_Name", "Name");
            table.CreateIndex("IDX_SurveyIndex_Status", "Status", "Kind");
            table.CreateIndex("IDX_SurveyIndex_Owner", "OwnerRef");
            table.CreateIndex("IDX_SurveyIndex_Modified", "ModifiedUtc", "Id");
            table.CreateIndex("IDX_SurveyIndex_OwnerId", "OwnerId");
            table.CreateIndex("IDX_SurveyIndex_Retention", "RetentionDays");
        }, collection: collection);

        await CreateSurveyTagTableAsync(schemaBuilder);
        await CreateInvitationOptOutTableAsync(schemaBuilder);

        await schemaBuilder.CreateMapIndexTableAsync<SurveyTemplateIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(100))
            .Column<string>("Title", column => column.Nullable().WithLength(255))
            .Column<string>("Kind", column => column.Nullable().WithLength(50))
            .Column<int>("Position"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<SurveyTemplateIndex>(table =>
        {
            table.CreateIndex("IDX_SurveyTemplateIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_SurveyTemplateIndex_Name", "Name");
            table.CreateIndex("IDX_SurveyTemplateIndex_Position", "Position", "Title");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<SurveyVersionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("SurveyId", column => column.Nullable().WithLength(26))
            .Column<int>("Number")
            .Column<DateTime>("PublishedUtc")
            .Column<string>("PublishedBy", column => column.Nullable().WithLength(26))
            .Column<string>("Notes", column => column.Nullable().WithLength(SurveyVersionIndex.NotesLength)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<SurveyVersionIndex>(table =>
        {
            table.CreateIndex("IDX_SurveyVersionIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_SurveyVersionIndex_Survey", "SurveyId", "Number");
            table.CreateIndex("IDX_SurveyVersionIndex_Published", "PublishedUtc", "Id");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<QuestionBankIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(100))
            .Column<string>("Title", column => column.Nullable().WithLength(255))
            .Column<string>("OwnerRef", column => column.Nullable().WithLength(255)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<QuestionBankIndex>(table =>
        {
            table.CreateIndex("IDX_QuestionBankIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_QuestionBankIndex_Name", "Name");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<BankQuestionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("BankId", column => column.Nullable().WithLength(26))
            .Column<string>("ControlType", column => column.Nullable().WithLength(100))
            .Column<string>("Title", column => column.Nullable().WithLength(255))
            .Column<int>("Difficulty")
            .Column<bool>("Retired")
            .Column<string>("SourceIdentifier", column => column.Nullable().WithLength(255))
            .Column<DateTime?>("ModifiedUtc", column => column.Nullable()),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<BankQuestionIndex>(table =>
        {
            table.CreateIndex("IDX_BankQuestionIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_BankQuestionIndex_Bank", "BankId", "Retired");
            table.CreateIndex("IDX_BankQuestionIndex_Source", "SourceIdentifier");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<BankQuestionTagIndex>(table => table
            .Column<string>("BankQuestionId", column => column.Nullable().WithLength(26))
            .Column<string>("BankId", column => column.Nullable().WithLength(26))
            .Column<string>("Tag", column => column.Nullable().WithLength(100))
            .Column<bool>("Retired"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<BankQuestionTagIndex>(table =>
        {
            table.CreateIndex("IDX_BankQuestionTagIndex_Tag", "BankId", "Tag");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<SurveyCollectorIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("SurveyId", column => column.Nullable().WithLength(26))
            .Column<string>("Kind", column => column.Nullable().WithLength(50))
            .Column<string>("Token", column => column.Nullable().WithLength(64))
            .Column<string>("Status", column => column.Nullable().WithLength(20))
            .Column<bool>("IsDefault"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<SurveyCollectorIndex>(table =>
        {
            table.CreateIndex("IDX_SurveyCollectorIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_SurveyCollectorIndex_Token", "Token");
            table.CreateIndex("IDX_SurveyCollectorIndex_Survey", "SurveyId", "IsDefault");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<SurveyResponseIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("SurveyId", column => column.Nullable().WithLength(26))
            .Column<string>("SurveyVersionId", column => column.Nullable().WithLength(26))
            .Column<int>("VersionNumber")
            .Column<string>("Number", column => column.Nullable().WithLength(32))
            .Column<string>("Status", column => column.Nullable().WithLength(20))
            .Column<string>("Grading", column => column.Nullable().WithLength(20))
            .Column<string>("UserId", column => column.Nullable().WithLength(26))
            .Column<string>("RespondentRef", column => column.Nullable().WithLength(255))
            .Column<string>("ContextRef", column => column.Nullable().WithLength(255))
            .Column<string>("ContextKind", column => column.Nullable().WithLength(100))
            .Column<string>("CollectorId", column => column.Nullable().WithLength(26))
            .Column<string>("InvitationId", column => column.Nullable().WithLength(26))
            .Column<int>("AttemptNumber")
            .Column<bool>("IsTest")
            .Column<DateTime>("StartedUtc")
            .Column<DateTime>("LastActivityUtc")
            .Column<DateTime?>("CompletedUtc", column => column.Nullable())
            .Column<DateTime?>("DeadlineUtc", column => column.Nullable())
            .Column<decimal>("Percent", column => column.Nullable())
            .Column<bool?>("Passed", column => column.Nullable())
            .Column<DateTime>("ModifiedUtc")
            .Column<bool>("Anonymized", column => column.NotNull().WithDefault(false))
            .Column<int>("UpkeepFailures", column => column.NotNull().WithDefault(0)),
            collection: responses);

        await schemaBuilder.AlterIndexTableAsync<SurveyResponseIndex>(table =>
        {
            table.CreateIndex("IDX_SurveyResponseIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_SurveyResponseIndex_SurveyCompleted", "SurveyId", "CompletedUtc", "Id");
            table.CreateIndex("IDX_SurveyResponseIndex_User", "UserId", "SurveyId");
            table.CreateIndex("IDX_SurveyResponseIndex_Context", "ContextRef", "SurveyId");
            table.CreateIndex("IDX_SurveyResponseIndex_Activity", "Status", "LastActivityUtc");
            table.CreateIndex("IDX_SurveyResponseIndex_Grading", "Grading", "SurveyId");
            table.CreateIndex("IDX_SurveyResponseIndex_Deadline", "DeadlineUtc", "Status");
            table.CreateIndex("IDX_SurveyResponseIndex_Modified", "ModifiedUtc", "Id");
            table.CreateIndex("IDX_SurveyResponseIndex_Started", "StartedUtc", "Id");
        }, collection: responses);

        await schemaBuilder.CreateMapIndexTableAsync<SurveyParticipationIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("SurveyId", column => column.Nullable().WithLength(26))
            .Column<string>("RespondentKey", column => column.Nullable().WithLength(64))
            .Column<string>("ContextKey", column => column.Nullable().WithLength(255))
            .Column<DateTime>("Day")
            .Column<int>("StartedCount")
            .Column<int>("CompletedCount"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<SurveyParticipationIndex>(table =>
        {
            table.CreateIndex("IDX_SurveyParticipationIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_SurveyParticipationIndex_Respondent", "SurveyId", "RespondentKey", "Day");
            table.CreateIndex("IDX_SurveyParticipationIndex_Day", "Day");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<SurveyInvitationIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("SurveyId", column => column.Nullable().WithLength(26))
            .Column<string>("CollectorId", column => column.Nullable().WithLength(26))
            .Column<string>("Status", column => column.Nullable().WithLength(20))
            .Column<string>("UserId", column => column.Nullable().WithLength(26))
            .Column<string>("RecipientRef", column => column.Nullable().WithLength(255))
            .Column<string>("TokenHash", column => column.Nullable().WithLength(64))
            .Column<string>("SealLookup", column => column.Nullable().WithLength(64))
            .Column<string>("Email", column => column.Nullable().WithLength(254))
            .Column<DateTime?>("LastReminderUtc", column => column.Nullable())
            .Column<DateTime?>("SentUtc", column => column.Nullable())
            .Column<DateTime?>("ExpiresUtc", column => column.Nullable())
            .Column<bool>("Held", column => column.NotNull().WithDefault(false))
            .Column<int>("RemindersSent", column => column.NotNull().WithDefault(0))
            .Column<bool>("RemindersStopped", column => column.NotNull().WithDefault(false)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<SurveyInvitationIndex>(table =>
        {
            table.CreateIndex("IDX_SurveyInvitationIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_SurveyInvitationIndex_Status", "SurveyId", "Status");
            table.CreateIndex("IDX_SurveyInvitationIndex_Token", "TokenHash");
            table.CreateIndex("IDX_SurveyInvitationIndex_Seal", "CollectorId", "SealLookup");
            table.CreateIndex("IDX_SurveyInvitationIndex_Email", "Email");
            table.CreateIndex("IDX_SurveyInvitationIndex_Held", "CollectorId", "Held");
            table.CreateIndex("IDX_SurveyInvitationIndex_Remind", "CollectorId", "Status", "RemindersStopped", "RemindersSent");
            AddInvitationRecipientIndexes(table);
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<SurveyOverrideIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("SurveyId", column => column.Nullable().WithLength(26))
            .Column<string>("UserId", column => column.Nullable().WithLength(26))
            .Column<string>("AudienceId", column => column.Nullable().WithLength(26))
            .Column<string>("ContextRef", column => column.Nullable().WithLength(255))
            .Column<DateTime>("CreatedUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<SurveyOverrideIndex>(table =>
        {
            table.CreateIndex("IDX_SurveyOverrideIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_SurveyOverrideIndex_User", "SurveyId", "UserId");
        }, collection: collection);
    }

    /// <summary>
    /// Adds the seal lookup and e-mail columns of the invitation index to a site whose tables were made before they
    /// existed. Rows written before stay empty in them until the invitation is saved again.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task AddInvitationLookupColumnsAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.AlterIndexTableAsync<SurveyInvitationIndex>(table =>
        {
            table.AddColumn<string>("SealLookup", column => column.Nullable().WithLength(64));
            table.AddColumn<string>("Email", column => column.Nullable().WithLength(254));
            table.AddColumn<DateTime?>("LastReminderUtc", column => column.Nullable());
        }, collection: SurveysConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<SurveyInvitationIndex>(table =>
        {
            table.CreateIndex("IDX_SurveyInvitationIndex_Seal", "CollectorId", "SealLookup");
            table.CreateIndex("IDX_SurveyInvitationIndex_Email", "Email");
        }, collection: SurveysConstants.CollectionName);
    }

    /// <summary>
    /// Adds the hold column of the invitation index, so the reminder task reads held first sends directly. It is not
    /// null and defaults to not held, so every database (SQL Server included) fills the existing rows; an invitation held
    /// before the column existed is marked when it is saved again.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task AddInvitationHoldColumnAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.AlterIndexTableAsync<SurveyInvitationIndex>(table =>
            table.AddColumn<bool>("Held", column => column.NotNull().WithDefault(false)),
            collection: SurveysConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<SurveyInvitationIndex>(table =>
            table.CreateIndex("IDX_SurveyInvitationIndex_Held", "CollectorId", "Held"),
            collection: SurveysConstants.CollectionName);
    }

    /// <summary>
    /// Adds the reminder columns of the invitation index (how many reminders were sent, and whether they stopped), so
    /// the reminder task reads only the invitations due a reminder instead of every open one. They are not null and
    /// default to none sent and not stopped, so every database fills the existing rows; the step that indexes the
    /// reminded invitations again fills the count of those that had reminders.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task AddInvitationReminderColumnsAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.AlterIndexTableAsync<SurveyInvitationIndex>(table =>
        {
            table.AddColumn<int>("RemindersSent", column => column.NotNull().WithDefault(0));
            table.AddColumn<bool>("RemindersStopped", column => column.NotNull().WithDefault(false));
        }, collection: SurveysConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<SurveyInvitationIndex>(table =>
            table.CreateIndex("IDX_SurveyInvitationIndex_Remind", "CollectorId", "Status", "RemindersStopped", "RemindersSent"),
            collection: SurveysConstants.CollectionName);
    }

    /// <summary>
    /// Adds what the survey list and the access checks read without loading surveys: the user owner and the anonymity
    /// columns of the survey index, and the survey tag index. Rows indexed before have an empty anonymity, so readers
    /// fall back to the survey until the step that indexes the surveys again reaches them.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task AddSurveyListColumnsAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.AlterIndexTableAsync<SurveyIndex>(table =>
        {
            table.AddColumn<string>("OwnerId", column => column.Nullable().WithLength(26));
            table.AddColumn<string>("Anonymity", column => column.Nullable().WithLength(20));
            table.AddColumn<int?>("MinimumGroupSize", column => column.Nullable());
        }, collection: SurveysConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<SurveyIndex>(table =>
            table.CreateIndex("IDX_SurveyIndex_OwnerId", "OwnerId"),
            collection: SurveysConstants.CollectionName);

        await CreateSurveyTagTableAsync(schemaBuilder);
    }

    /// <summary>
    /// Adds what upkeep reads without loading every survey: the retention days, the closing date and the total limit of
    /// the survey index. Rows indexed before have empty retention days, so upkeep reads those surveys themselves until
    /// the step that indexes the surveys again reaches them.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task AddSurveyUpkeepColumnsAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.AlterIndexTableAsync<SurveyIndex>(table =>
        {
            table.AddColumn<int?>("RetentionDays", column => column.Nullable());
            table.AddColumn<DateTime?>("ClosesUtc", column => column.Nullable());
            table.AddColumn<int?>("TotalLimit", column => column.Nullable());
        }, collection: SurveysConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<SurveyIndex>(table =>
            table.CreateIndex("IDX_SurveyIndex_Retention", "RetentionDays"),
            collection: SurveysConstants.CollectionName);
    }

    /// <summary>
    /// Adds the delete mark of the survey index, so a start reads it by the index (R8-Mi5). Schema only: a survey marked
    /// as being deleted when the column is added is still read as not marked by a start until it is saved again; the
    /// delete then finishes by its own follow-up, and its counts after the mark still stop it for a response that
    /// started meanwhile.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task AddSurveyDeletingColumnAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.AlterIndexTableAsync<SurveyIndex>(table =>
            table.AddColumn<DateTime?>("DeletingUtc", column => column.Nullable()),
            collection: SurveysConstants.CollectionName);
    }

    /// <summary>
    /// Creates the index of the survey invitation opt-out records, one per address that unsubscribed, found by the hash
    /// of the address.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateInvitationOptOutTableAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.CreateMapIndexTableAsync<SurveyInvitationOptOutIndex>(table => table
            .Column<string>("AddressHash", column => column.WithLength(SurveyInvitationOptOutIndex.HashLength)),
            collection: SurveysConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<SurveyInvitationOptOutIndex>(table =>
            table.CreateIndex("IDX_SurveyInvitationOptOutIndex_Hash", "AddressHash"),
            collection: SurveysConstants.CollectionName);
    }

    /// <summary>
    /// Adds what the versions list shows without loading versions: who published each version and its notes. Rows
    /// indexed before have an empty publisher, so the list reads those versions themselves until the step that indexes
    /// the versions again reaches them.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task AddVersionListColumnsAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.AlterIndexTableAsync<SurveyVersionIndex>(table =>
        {
            table.AddColumn<string>("PublishedBy", column => column.Nullable().WithLength(26));
            table.AddColumn<string>("Notes", column => column.Nullable().WithLength(SurveyVersionIndex.NotesLength));
        }, collection: SurveysConstants.CollectionName);
    }

    /// <summary>
    /// Adds the database indexes that find a list's invitation of a user or of a recipient (inviting someone again, and
    /// personal data export and erasure, which find a user's invitations), and that count the day's invitation e-mails
    /// by when they were sent or reminded. Indexes only: no column or row changes.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task AddInvitationRecipientIndexesAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.AlterIndexTableAsync<SurveyInvitationIndex>(AddInvitationRecipientIndexes, collection: SurveysConstants.CollectionName);
    }

    internal static void AddInvitationRecipientIndexes(IAlterTableCommand table)
    {
        table.CreateIndex("IDX_SurveyInvitationIndex_User", "UserId", "CollectorId");
        table.CreateIndex("IDX_SurveyInvitationIndex_Recipient", "RecipientRef", "CollectorId");
        table.CreateIndex("IDX_SurveyInvitationIndex_Sent", "SentUtc");
        table.CreateIndex("IDX_SurveyInvitationIndex_Reminded", "LastReminderUtc");
    }

    private static async Task CreateSurveyTagTableAsync(ISchemaBuilder schemaBuilder)
    {
        await schemaBuilder.CreateMapIndexTableAsync<SurveyTagIndex>(table => table
            .Column<string>("SurveyId", column => column.WithLength(26))
            .Column<string>("Tag", column => column.WithLength(SurveyTagIndexProvider.MaxTagLength))
            .Column<string>("OwnerId", column => column.Nullable().WithLength(26)),
            collection: SurveysConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<SurveyTagIndex>(table =>
        {
            table.CreateIndex("IDX_SurveyTagIndex_Tag", "Tag", "DocumentId");
            table.CreateIndex("IDX_SurveyTagIndex_Owner", "OwnerId", "Tag");
        }, collection: SurveysConstants.CollectionName);
    }

    /// <summary>
    /// Adds the upkeep columns of the response index: whether retention anonymized a response, and how many times in a
    /// row upkeep failed on it, so upkeep skips both without reading them. The columns are not nullable, so every
    /// database (SQL Server included) fills the rows already in the table with the default.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task AddResponseUpkeepColumnsAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.AlterIndexTableAsync<SurveyResponseIndex>(AddResponseUpkeepColumns, collection: SurveysConstants.ResponsesCollectionName);
    }

    /// <summary>
    /// Sets the upkeep columns of the response index to their defaults where they are empty. A site whose columns were
    /// added as nullable on SQL Server has empty values in the rows that were there before, which upkeep's filters on
    /// them never match.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <param name="schema">The database schema of the tenant's tables, or <see langword="null"/>.</param>
    /// <returns>How many values were set.</returns>
    public static async Task<int> FillResponseUpkeepColumnsAsync(ISchemaBuilder schemaBuilder, string schema)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        var dialect = schemaBuilder.Dialect;
        var table = dialect.QuoteForTableName(
            schemaBuilder.TablePrefix + schemaBuilder.TableNameConvention.GetIndexTable(typeof(SurveyResponseIndex), SurveysConstants.ResponsesCollectionName),
            schema);
        var filled = 0;

        foreach (var (column, value) in new (string Column, object Value)[] { ("Anonymized", false), ("UpkeepFailures", 0) })
        {
            var quoted = dialect.QuoteForColumnName(column);

            filled += await schemaBuilder.Connection.ExecuteAsync(
                $"UPDATE {table} SET {quoted} = @Value WHERE {quoted} IS NULL",
                new { Value = value },
                schemaBuilder.Transaction);
        }

        return filled;
    }

    internal static void AddResponseUpkeepColumns(IAlterTableCommand table)
    {
        table.AddColumn<bool>("Anonymized", column => column.NotNull().WithDefault(false));
        table.AddColumn<int>("UpkeepFailures", column => column.NotNull().WithDefault(0));
    }
}
