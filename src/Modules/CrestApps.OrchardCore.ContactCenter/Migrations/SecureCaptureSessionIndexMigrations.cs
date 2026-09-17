using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Migrations;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="SecureCaptureSessionIndex"/>.
/// </summary>
internal sealed class SecureCaptureSessionIndexMigrations : DataMigration
{
    private readonly SecureCaptureSessionIndexMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecureCaptureSessionIndexMigrations"/> class.
    /// </summary>
    public SecureCaptureSessionIndexMigrations()
    {
        _step = new SecureCaptureSessionIndexMigrationsSchemaMigration();
    }

    /// <summary>
    /// Creates the secure capture session index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);
}
