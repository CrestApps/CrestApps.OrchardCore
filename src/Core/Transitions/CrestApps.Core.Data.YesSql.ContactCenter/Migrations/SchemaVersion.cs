namespace CrestApps.Core.Data.YesSql.Migrations;

/// <summary>
/// Records which version of each schema migration a database has had applied.
/// </summary>
/// <remarks>
/// Keyed by <see cref="ISchemaMigration.Name"/> rather than by CLR type, so moving a migration
/// between assemblies or namespaces does not make an applied migration look unapplied.
/// </remarks>
public sealed class SchemaVersion
{
    /// <summary>
    /// Gets the applied version of each migration, by name.
    /// </summary>
    public Dictionary<string, int> AppliedVersions { get; init; } = new(StringComparer.Ordinal);
}
