using CrestApps.OrchardCore.Learning.Certifications.Core.Indexes;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Learning.Certifications.Migrations;

/// <summary>
/// Creates the tables of certification paths, progress and the renewal queue.
/// </summary>
internal sealed class CertificationsMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await CertificationsIndexSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}
