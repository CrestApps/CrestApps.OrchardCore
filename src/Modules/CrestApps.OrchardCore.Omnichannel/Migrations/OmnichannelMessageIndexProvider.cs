using CrestApps.Core.Data.YesSql.Omnichannel.Indexes;
using CrestApps.Core.Data.YesSql.Omnichannel.Migrations;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Omnichannel.Migrations;

/// <summary>
/// Creates the schema for the <see cref="OmnichannelMessageIndex"/>.
/// </summary>
internal sealed class OmnichannelMessageIndexMigrations : DataMigration
{
    private readonly OmnichannelMessageIndexMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelMessageIndexMigrations"/> class.
    /// </summary>
    public OmnichannelMessageIndexMigrations()
    {
        _step = new OmnichannelMessageIndexMigrationsSchemaMigration();
    }

    /// <summary>
    /// Creates the message index table and its supporting indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);

    /// <summary>
    /// Adds the <c>ConversationId</c> column and its index so the SMS portal can load a thread's message
    /// bubbles by conversation.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom1Async()
        => _step.UpdateFromAsync(1, SchemaBuilder);

    /// <summary>
    /// Adds the <c>ProviderMessageId</c> column and its index, so a delivery receipt matches the message it
    /// belongs to in one indexed seek instead of scanning a thread's outbound history, and a redelivered
    /// provider message is recognised as one already stored.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom2Async()
        => _step.UpdateFromAsync(2, SchemaBuilder);
}
