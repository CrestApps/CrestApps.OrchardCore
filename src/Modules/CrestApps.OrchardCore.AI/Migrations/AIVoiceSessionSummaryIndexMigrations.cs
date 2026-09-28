using CrestApps.Core.Data.YesSql;
using CrestApps.OrchardCore.AI.Core.Indexes;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

/// <summary>
/// Creates the table the per-call AI voice session summaries are reported from.
/// </summary>
internal sealed class AIVoiceSessionSummaryIndexMigrations : DataMigration
{
    private readonly YesSqlStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIVoiceSessionSummaryIndexMigrations"/> class.
    /// </summary>
    /// <param name="options">The store options that name the AI collection.</param>
    public AIVoiceSessionSummaryIndexMigrations(IOptions<YesSqlStoreOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Creates the index table.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await AIVoiceSessionSummaryIndexSchema.CreateAsync(SchemaBuilder, _options);

        return 1;
    }
}
