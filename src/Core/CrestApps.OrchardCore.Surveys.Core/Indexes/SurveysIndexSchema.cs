using YesSql.Sql;

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
            .Column<DateTime?>("ModifiedUtc", column => column.Nullable()),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<SurveyIndex>(table =>
        {
            table.CreateIndex("IDX_SurveyIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_SurveyIndex_Name", "Name");
            table.CreateIndex("IDX_SurveyIndex_Status", "Status", "Kind");
            table.CreateIndex("IDX_SurveyIndex_Owner", "OwnerRef");
            table.CreateIndex("IDX_SurveyIndex_Modified", "ModifiedUtc", "Id");
        }, collection: collection);

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
            .Column<DateTime>("PublishedUtc"),
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
            .Column<bool>("Anonymized", column => column.WithDefault(false))
            .Column<int>("UpkeepFailures", column => column.WithDefault(0)),
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
            .Column<DateTime?>("ExpiresUtc", column => column.Nullable()),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<SurveyInvitationIndex>(table =>
        {
            table.CreateIndex("IDX_SurveyInvitationIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_SurveyInvitationIndex_Status", "SurveyId", "Status");
            table.CreateIndex("IDX_SurveyInvitationIndex_Token", "TokenHash");
            table.CreateIndex("IDX_SurveyInvitationIndex_Seal", "CollectorId", "SealLookup");
            table.CreateIndex("IDX_SurveyInvitationIndex_Email", "Email");
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
    /// Adds the upkeep columns of the response index: whether retention anonymized a response, and how many times in a
    /// row upkeep failed on it, so upkeep skips both without reading them.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task AddResponseUpkeepColumnsAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.AlterIndexTableAsync<SurveyResponseIndex>(table =>
        {
            table.AddColumn<bool>("Anonymized", column => column.WithDefault(false));
            table.AddColumn<int>("UpkeepFailures", column => column.WithDefault(0));
        }, collection: SurveysConstants.ResponsesCollectionName);
    }
}
