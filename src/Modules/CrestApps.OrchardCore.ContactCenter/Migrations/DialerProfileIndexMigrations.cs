using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.Core.Data.YesSql.ContactCenter.Migrations;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="DialerProfileIndex"/>. A dialer profile is reusable dialing settings:
/// it does not own a campaign or queue (those are chosen when inventory is loaded), so the index carries only the
/// name and enabled flag used to list and pace profiles.
/// </summary>
internal sealed class DialerProfileIndexMigrations : DataMigration
{
    private readonly DialerProfileIndexMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerProfileIndexMigrations"/> class.
    /// </summary>
    public DialerProfileIndexMigrations()
    {
        _step = new DialerProfileIndexMigrationsSchemaMigration();
    }

    /// <summary>
    /// Creates the dialer profile index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);
}
