using CrestApps.OrchardCore.Telephony.Core.Migrations;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Telephony.Migrations;

/// <summary>
/// Creates the schema used to resolve provider events to connected Orchard users.
/// </summary>
/// <remarks>
/// The schema work itself lives in <see cref="TelephonyUserConnectionIndexSchemaMigration"/>. This class
/// stays only to give Orchard something to discover: Orchard records the applied version under this type's
/// full name, so renaming or removing it would make the create step run again against existing tables.
/// </remarks>
public sealed class TelephonyUserConnectionIndexMigrations : DataMigration
{
    private readonly TelephonyUserConnectionIndexSchemaMigration _step = new();

    /// <summary>
    /// Creates the telephony user-connection index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);
}
