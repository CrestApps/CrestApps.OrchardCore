using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Learning.Xapi.Models;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Learning.Xapi.Core.Indexes;

/// <summary>
/// One row per statement: what the admin list, the data source, personal data and the read scopes filter on. IRIs and
/// agent keys are kept as SHA-256 hashes, because an IRI may be longer than a database index allows.
/// </summary>
public sealed class XapiStatementIndex : CatalogItemIndex
{
    /// <summary>Gets or sets the statement id.</summary>
    public string StatementId { get; set; }

    /// <summary>Gets or sets the position of the statement in the order statements were stored in.</summary>
    public long Sequence { get; set; }

    /// <summary>Gets or sets when the statement was stored, in UTC.</summary>
    public DateTime StoredUtc { get; set; }

    /// <summary>Gets or sets the statement's timestamp, in UTC.</summary>
    public DateTime? TimestampUtc { get; set; }

    /// <summary>Gets or sets whether the statement was voided.</summary>
    public bool Voided { get; set; }

    /// <summary>Gets or sets whether the statement voids another.</summary>
    public bool IsVoiding { get; set; }

    /// <summary>Gets or sets the statement the object refers to.</summary>
    public string TargetStatementId { get; set; }

    /// <summary>Gets or sets the hash of the verb id.</summary>
    public string VerbHash { get; set; }

    /// <summary>Gets or sets the hash of the object id.</summary>
    public string ObjectHash { get; set; }

    /// <summary>Gets or sets the hash of the actor key.</summary>
    public string ActorHash { get; set; }

    /// <summary>Gets or sets the account name of the actor, when it has an account and the name fits.</summary>
    public string ActorAccountName { get; set; }

    /// <summary>Gets or sets the registration.</summary>
    public string Registration { get; set; }

    /// <summary>Gets or sets the hash of the authority key.</summary>
    public string AuthorityHash { get; set; }
}

/// <summary>
/// One row per agent a statement query can match a statement by, with whether it matches without
/// <c>related_agents</c>; the statement's own agents and those inherited through statement references.
/// </summary>
public sealed class XapiStatementAgentIndex : MapIndex
{
    /// <summary>Gets or sets the hash of the agent key.</summary>
    public string KeyHash { get; set; }

    /// <summary>Gets or sets whether the agent matches without <c>related_agents</c>.</summary>
    public bool Direct { get; set; }
}

/// <summary>
/// One row per activity a statement query can match a statement by, with whether it matches without
/// <c>related_activities</c>.
/// </summary>
public sealed class XapiStatementActivityIndex : MapIndex
{
    /// <summary>Gets or sets the hash of the activity id.</summary>
    public string ActivityHash { get; set; }

    /// <summary>Gets or sets whether the activity matches without <c>related_activities</c>.</summary>
    public bool Direct { get; set; }
}

/// <summary>
/// One row per verb a statement query can match a statement by.
/// </summary>
public sealed class XapiStatementVerbIndex : MapIndex
{
    /// <summary>Gets or sets the hash of the verb id.</summary>
    public string VerbHash { get; set; }
}

/// <summary>
/// One row per registration a statement query can match a statement by.
/// </summary>
public sealed class XapiStatementRegistrationIndex : MapIndex
{
    /// <summary>Gets or sets the registration.</summary>
    public string Registration { get; set; }
}

/// <summary>
/// Maps statements to their indexes.
/// </summary>
public sealed class XapiStatementIndexProvider : IndexProvider<XapiStatementRecord>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="XapiStatementIndexProvider"/> class.
    /// </summary>
    public XapiStatementIndexProvider()
    {
        CollectionName = XapiConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<XapiStatementRecord> context)
    {
        context.For<XapiStatementIndex>()
            .Map(record => new XapiStatementIndex
            {
                ItemId = record.ItemId,
                StatementId = record.StatementId,
                Sequence = record.Sequence,
                StoredUtc = record.StoredUtc,
                TimestampUtc = record.TimestampUtc,
                Voided = record.Voided,
                IsVoiding = record.IsVoiding,
                TargetStatementId = record.TargetStatementId,
                VerbHash = XapiAgents.Hash(record.Verb),
                ObjectHash = XapiAgents.Hash(record.ObjectId),
                ActorHash = XapiAgents.Hash(record.ActorKey),
                ActorAccountName = record.ActorAccountName?.Length <= 255 ? record.ActorAccountName : null,
                Registration = record.Registration,
                AuthorityHash = XapiAgents.Hash(record.AuthorityKey),
            });

        context.For<XapiStatementAgentIndex>()
            .Map(record => (record.Keys?.Agents ?? [])
                .Where(key => !string.IsNullOrEmpty(key.Value))
                .GroupBy(key => key.Value, StringComparer.Ordinal)
                .Select(group => new XapiStatementAgentIndex
                {
                    KeyHash = XapiAgents.Hash(group.Key),
                    Direct = group.Any(key => key.Direct),
                }));

        context.For<XapiStatementActivityIndex>()
            .Map(record => (record.Keys?.Activities ?? [])
                .Where(key => !string.IsNullOrEmpty(key.Value))
                .GroupBy(key => key.Value, StringComparer.Ordinal)
                .Select(group => new XapiStatementActivityIndex
                {
                    ActivityHash = XapiAgents.Hash(group.Key),
                    Direct = group.Any(key => key.Direct),
                }));

        context.For<XapiStatementVerbIndex>()
            .Map(record => (record.Keys?.Verbs ?? [])
                .Where(verb => !string.IsNullOrEmpty(verb))
                .Distinct(StringComparer.Ordinal)
                .Select(verb => new XapiStatementVerbIndex { VerbHash = XapiAgents.Hash(verb) }));

        context.For<XapiStatementRegistrationIndex>()
            .Map(record => (record.Keys?.Registrations ?? [])
                .Where(registration => !string.IsNullOrEmpty(registration))
                .Distinct(StringComparer.Ordinal)
                .Select(registration => new XapiStatementRegistrationIndex { Registration = registration }));
    }
}

/// <summary>
/// Creates the index tables of the Learning Record Store.
/// </summary>
public static class XapiIndexSchema
{
    /// <summary>
    /// Gets the index providers of the Learning Record Store.
    /// </summary>
    /// <returns>The providers.</returns>
    public static IEnumerable<IIndexProvider> IndexProviders()
    {
        yield return new XapiStatementIndexProvider();
    }

    /// <summary>
    /// Creates every index table of the Learning Record Store.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAllAsync(ISchemaBuilder schemaBuilder)
    {
        await CreateStatementTablesAsync(schemaBuilder);
    }

    /// <summary>
    /// Creates the statement index tables and their database indexes.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateStatementTablesAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = XapiConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<XapiStatementIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("StatementId", column => column.WithLength(36))
            .Column<long>("Sequence")
            .Column<DateTime>("StoredUtc")
            .Column<DateTime>("TimestampUtc", column => column.Nullable())
            .Column<bool>("Voided")
            .Column<bool>("IsVoiding")
            .Column<string>("TargetStatementId", column => column.Nullable().WithLength(36))
            .Column<string>("VerbHash", column => column.Nullable().WithLength(64))
            .Column<string>("ObjectHash", column => column.Nullable().WithLength(64))
            .Column<string>("ActorHash", column => column.Nullable().WithLength(64))
            .Column<string>("ActorAccountName", column => column.Nullable().WithLength(255))
            .Column<string>("Registration", column => column.Nullable().WithLength(36))
            .Column<string>("AuthorityHash", column => column.Nullable().WithLength(64)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<XapiStatementIndex>(table =>
        {
            table.CreateIndex("IDX_LearningXapi_Statement_ItemId", "ItemId");
            table.CreateIndex("IDX_LearningXapi_Statement_StatementId", "StatementId");
            table.CreateIndex("IDX_LearningXapi_Statement_Stored", "StoredUtc", "Sequence");
            table.CreateIndex("IDX_LearningXapi_Statement_Sequence", "Sequence");
            table.CreateIndex("IDX_LearningXapi_Statement_Target", "TargetStatementId", "IsVoiding");
            table.CreateIndex("IDX_LearningXapi_Statement_Actor", "ActorHash", "StoredUtc");
            table.CreateIndex("IDX_LearningXapi_Statement_Account", "ActorAccountName");
            table.CreateIndex("IDX_LearningXapi_Statement_Verb", "VerbHash", "StoredUtc");
            table.CreateIndex("IDX_LearningXapi_Statement_Object", "ObjectHash", "StoredUtc");
            table.CreateIndex("IDX_LearningXapi_Statement_Registration", "Registration", "StoredUtc");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<XapiStatementAgentIndex>(table => table
            .Column<string>("KeyHash", column => column.WithLength(64))
            .Column<bool>("Direct"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<XapiStatementAgentIndex>(table => table
            .CreateIndex("IDX_LearningXapi_StatementAgent_Key", "KeyHash", "Direct", "DocumentId"), collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<XapiStatementActivityIndex>(table => table
            .Column<string>("ActivityHash", column => column.WithLength(64))
            .Column<bool>("Direct"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<XapiStatementActivityIndex>(table => table
            .CreateIndex("IDX_LearningXapi_StatementActivity_Key", "ActivityHash", "Direct", "DocumentId"), collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<XapiStatementVerbIndex>(table => table
            .Column<string>("VerbHash", column => column.WithLength(64)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<XapiStatementVerbIndex>(table => table
            .CreateIndex("IDX_LearningXapi_StatementVerb_Key", "VerbHash", "DocumentId"), collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<XapiStatementRegistrationIndex>(table => table
            .Column<string>("Registration", column => column.WithLength(36)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<XapiStatementRegistrationIndex>(table => table
            .CreateIndex("IDX_LearningXapi_StatementRegistration_Key", "Registration", "DocumentId"), collection: collection);
    }
}
