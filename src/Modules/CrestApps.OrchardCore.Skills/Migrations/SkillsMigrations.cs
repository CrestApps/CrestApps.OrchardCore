using CrestApps.OrchardCore.Skills.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql;

namespace CrestApps.OrchardCore.Skills.Migrations;

internal sealed class SkillsMigrations : DataMigration
{
    private readonly IStore _store;

    public SkillsMigrations(IStore store)
    {
        _store = store;
    }

    public async Task<int> CreateAsync()
    {
        await SkillsIndexSchema.CreateAsync(SchemaBuilder);

        return 2;
    }

    /// <summary>
    /// Makes the kind of record that holds a skill an open string: adds the <c>SubjectKindName</c> column to the
    /// assignment and certification record indexes and fills it in from the number older rows hold. The number column
    /// stays, so nothing is dropped.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SkillsIndexSchema.AddSubjectKindNameColumnsAsync(SchemaBuilder);
        await SkillsIndexSchema.BackfillSubjectKindNamesAsync(SchemaBuilder, _store);

        return 2;
    }
}
