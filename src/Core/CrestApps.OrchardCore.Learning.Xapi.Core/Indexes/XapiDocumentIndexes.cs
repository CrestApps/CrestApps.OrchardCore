using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Learning.Xapi.Models;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Learning.Xapi.Core.Indexes;

/// <summary>
/// One row per document: its kind, the hashes of its activity, agent and id, its registration and when it changed.
/// </summary>
public sealed class XapiDocumentIndex : CatalogItemIndex
{
    /// <summary>Gets or sets the kind.</summary>
    public string Kind { get; set; }

    /// <summary>Gets or sets the hash of the activity id.</summary>
    public string ActivityHash { get; set; }

    /// <summary>Gets or sets the hash of the agent key.</summary>
    public string AgentHash { get; set; }

    /// <summary>Gets or sets the registration.</summary>
    public string Registration { get; set; }

    /// <summary>
    /// Gets or sets the account name of the agent, whatever its home page, so that a person's documents are found after
    /// the site's address changed.
    /// </summary>
    public string AccountName { get; set; }

    /// <summary>Gets or sets the hash of the state id or profile id.</summary>
    public string DocumentHash { get; set; }

    /// <summary>Gets or sets when the document last changed, in UTC.</summary>
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>
/// One row per canonical activity definition.
/// </summary>
public sealed class XapiActivityIndex : CatalogItemIndex
{
    /// <summary>Gets or sets the hash of the activity id.</summary>
    public string ActivityHash { get; set; }
}

/// <summary>
/// Maps documents.
/// </summary>
public sealed class XapiDocumentIndexProvider : IndexProvider<XapiDocument>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="XapiDocumentIndexProvider"/> class.
    /// </summary>
    public XapiDocumentIndexProvider()
    {
        CollectionName = XapiConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<XapiDocument> context)
        => context.For<XapiDocumentIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(document => new XapiDocumentIndex
            {
                ItemId = document.ItemId,
                Kind = document.Kind,
                ActivityHash = XapiAgents.Hash(document.ActivityId),
                AgentHash = XapiAgents.Hash(document.AgentKey),
                Registration = document.Registration,
                AccountName = XapiAgents.AccountName(document.Agent) is { Length: <= 255 } accountName ? accountName : null,
                DocumentHash = XapiAgents.Hash(document.DocumentId),
                UpdatedUtc = document.UpdatedUtc,
            });
}

/// <summary>
/// Maps canonical activity definitions.
/// </summary>
public sealed class XapiActivityIndexProvider : IndexProvider<XapiActivityRecord>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="XapiActivityIndexProvider"/> class.
    /// </summary>
    public XapiActivityIndexProvider()
    {
        CollectionName = XapiConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<XapiActivityRecord> context)
        => context.For<XapiActivityIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(activity => new XapiActivityIndex
            {
                ItemId = activity.ItemId,
                ActivityHash = XapiAgents.Hash(activity.ActivityId),
            });
}

/// <summary>
/// Creates the document and activity index tables.
/// </summary>
public static class XapiDocumentIndexSchema
{
    /// <summary>
    /// Creates the tables and their database indexes.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = XapiConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<XapiDocumentIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Kind", column => column.WithLength(20))
            .Column<string>("ActivityHash", column => column.Nullable().WithLength(64))
            .Column<string>("AgentHash", column => column.Nullable().WithLength(64))
            .Column<string>("Registration", column => column.Nullable().WithLength(36))
            .Column<string>("AccountName", column => column.Nullable().WithLength(255))
            .Column<string>("DocumentHash", column => column.WithLength(64))
            .Column<DateTime>("UpdatedUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<XapiDocumentIndex>(table =>
        {
            table.CreateIndex("IDX_LearningXapi_Document_ItemId", "ItemId");
            table.CreateIndex("IDX_LearningXapi_Document_Key", "Kind", "ActivityHash", "AgentHash", "DocumentHash");
            table.CreateIndex("IDX_LearningXapi_Document_Agent", "AgentHash");
            table.CreateIndex("IDX_LearningXapi_Document_Account", "AccountName");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<XapiActivityIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ActivityHash", column => column.WithLength(64)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<XapiActivityIndex>(table => table
            .CreateIndex("IDX_LearningXapi_Activity_Hash", "ActivityHash"), collection: collection);
    }

    /// <summary>
    /// Adds the account name column of the document index to a table made before it existed.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task AddAccountNameAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.AlterIndexTableAsync<XapiDocumentIndex>(table =>
        {
            table.AddColumn<string>("AccountName", column => column.Nullable().WithLength(255));
            table.CreateIndex("IDX_LearningXapi_Document_Account", "AccountName");
        }, collection: XapiConstants.CollectionName);
    }
}
