using CrestApps.OrchardCore.Telephony.Core.Migrations;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Telephony.Migrations;

/// <summary>
/// Creates the schema used to store telephony interactions for history and reporting.
/// </summary>
/// <remarks>
/// The schema work itself lives in <see cref="TelephonyInteractionSchemaMigration"/>. This class stays only
/// to give Orchard something to discover: Orchard records the applied version under this type's full name,
/// so renaming or removing it would make the create step run again against existing tables.
/// </remarks>
public sealed class TelephonyInteractionMigrations : DataMigration
{
    private readonly TelephonyInteractionSchemaMigration _step = new();

    /// <summary>
    /// Creates the telephony interaction index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);

    /// <summary>
    /// Adds the voicemail columns to an existing telephony interaction index so a call sent to voicemail can
    /// be surfaced (and its unread state tracked) in the soft phone's history.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom1Async()
        => _step.UpdateFromAsync(1, SchemaBuilder);

    /// <summary>
    /// Adds the extension-call marker column to an existing telephony interaction index so the Recent tab can
    /// redial an internal extension entry in extension mode.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> UpdateFrom2Async()
        => _step.UpdateFromAsync(2, SchemaBuilder);
}
