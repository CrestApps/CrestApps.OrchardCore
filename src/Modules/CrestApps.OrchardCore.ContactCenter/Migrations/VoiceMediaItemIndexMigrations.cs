using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Migrations;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="VoiceMediaItemIndex"/>.
/// </summary>
internal sealed class VoiceMediaItemIndexMigrations : DataMigration
{
    private readonly VoiceMediaItemIndexMigrationsSchemaMigration _step;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceMediaItemIndexMigrations"/> class.
    /// </summary>
    public VoiceMediaItemIndexMigrations()
    {
        _step = new VoiceMediaItemIndexMigrationsSchemaMigration();
    }

    /// <summary>
    /// Creates the voice media library index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public Task<int> CreateAsync()
        => _step.CreateAsync(SchemaBuilder);
}
