using CrestApps.OrchardCore.Telephony.Core.Indexes;
using CrestApps.OrchardCore.Telephony.Core.Migrations;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Telephony.Migrations;

/// <summary>
/// Creates the schema for the <see cref="TelephonyExtensionIndex"/>.
/// </summary>
/// <remarks>
/// The schema work itself lives in <see cref="TelephonyExtensionIndexSchemaMigration"/>. This class stays
/// only to give Orchard something to discover: Orchard records the applied version under this type's full
/// name, so renaming or removing it would make the create step run again against existing tables.
/// </remarks>
internal sealed class TelephonyExtensionIndexMigrations : DataMigration
{
    private readonly TelephonyExtensionIndexSchemaMigration _step = new();

    /// <summary>
    /// Creates the telephony extension index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);
}
