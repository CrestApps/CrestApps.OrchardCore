using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Migrations;
using Microsoft.Extensions.Logging;
using OrchardCore.Data.Migration;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Migrations;

/// <summary>
/// Creates the schema for the SMS Portal index tables (conversations, canned-response templates, and broadcasts).
/// </summary>
/// <remarks>
/// The schema itself lives in <see cref="SmsConversationMigrationsSchemaMigration"/>; this class only hands
/// Orchard's schema builder to it. The type name is part of the stored data, because Orchard records the
/// applied version under this class's full name, so it must not be renamed.
/// </remarks>
internal sealed class SmsConversationMigrations : DataMigration
{
    private readonly SmsConversationMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsConversationMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store, used to resolve the prefixed index table name.</param>
    /// <param name="logger">The logger.</param>
    public SmsConversationMigrations(
        IStore store,
        ILogger<SmsConversationMigrations> logger)
    {
        _step = new SmsConversationMigrationsSchemaMigration(store, logger);
    }

    /// <summary>
    /// Creates the SMS Portal index tables.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);

    /// <summary>
    /// Adds the unique index that keeps one conversation per number pair, so a concurrent create is refused by
    /// the database rather than producing a second thread for the same contact.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom1Async()
        => _step.UpdateFromAsync(1, SchemaBuilder);

    /// <summary>
    /// Adds the <c>UnreadCount</c> and <c>AssignedUtc</c> columns and the pickup index, so the inbox badge and
    /// the routed-pickup sweep are answered from the index instead of by loading and filtering every thread.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom2Async()
        => _step.UpdateFromAsync(2, SchemaBuilder);

    /// <summary>
    /// Adds the first-response deadline and the index the thirty-second sweep seeks on. Without the index the
    /// sweep reads every open conversation on the tenant twice a minute.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom3Async()
        => _step.UpdateFromAsync(3, SchemaBuilder);
}
