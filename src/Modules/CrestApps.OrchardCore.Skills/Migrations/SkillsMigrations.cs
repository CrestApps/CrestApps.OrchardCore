using CrestApps.OrchardCore.Skills.Core.Indexes;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using YesSql;

namespace CrestApps.OrchardCore.Skills.Migrations;

internal sealed class SkillsMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SkillsIndexSchema.CreateAsync(SchemaBuilder);

        return 3;
    }

    /// <summary>
    /// Makes the kind of record that holds a skill an open string: adds the <c>SubjectKindName</c> column to the
    /// assignment and certification record indexes. The number column stays, so nothing is dropped.
    /// </summary>
    /// <remarks>
    /// The new column is filled by mapping the stored records again after this step commits, not by a SQL update inside
    /// the migration: a failed statement aborts the whole migration transaction on some databases, and the records'
    /// own reader already understands the number older records hold. A failure is logged and leaves the tenant running;
    /// a record that was not mapped again gets its column the next time it is saved.
    /// </remarks>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SkillsIndexSchema.AddSubjectKindNameColumnsAsync(SchemaBuilder);

        ShellScope.AddDeferredTask(async scope =>
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SkillsMigrations>>();

            try
            {
                var session = scope.ServiceProvider.GetRequiredService<ISession>();

                await CatalogIndexBackfill.ReindexAsync<SkillAssignment, SkillAssignmentIndex>(session, SkillsConstants.CollectionName);
                await CatalogIndexBackfill.ReindexAsync<CertificationRecord, CertificationRecordIndex>(session, SkillsConstants.CollectionName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "The skill assignments and certification records could not be indexed again by subject kind. Records saved before the upgrade are indexed again when they are next saved.");
            }
        });

        return 2;
    }

    /// <summary>
    /// Workforce workers became staff members with the same ids: records held by a worker are read as held by a staff
    /// member, so they are mapped again to index them under the new kind name.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public int UpdateFrom2()
    {
        ShellScope.AddDeferredTask(async scope =>
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SkillsMigrations>>();

            try
            {
                var session = scope.ServiceProvider.GetRequiredService<ISession>();

                await CatalogIndexBackfill.ReindexAsync<SkillAssignment, SkillAssignmentIndex>(session, SkillsConstants.CollectionName);
                await CatalogIndexBackfill.ReindexAsync<CertificationRecord, CertificationRecordIndex>(session, SkillsConstants.CollectionName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "The skill assignments and certification records held by workers could not be indexed again as held by staff members. They are indexed again when they are next saved.");
            }
        });

        return 3;
    }
}
