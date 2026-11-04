using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.Core.Migrations;

/// <summary>
/// The migration skips an administrator has not dismissed yet.
/// </summary>
public sealed class MigrationSkipLogDocument : Document
{
    /// <summary>
    /// Gets or sets the skip summaries, oldest first.
    /// </summary>
    public List<MigrationSkipEntry> Entries { get; set; } = [];
}
