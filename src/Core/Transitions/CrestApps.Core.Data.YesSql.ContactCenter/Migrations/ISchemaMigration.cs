using YesSql.Sql;

namespace CrestApps.Core.Data.YesSql.Migrations;

/// <summary>
/// One versioned set of schema changes for a single concern.
/// </summary>
/// <remarks>
/// A host that has its own migration mechanism drives these from it; a host that has none runs them
/// through <see cref="SchemaMigrationRunner"/>. Either way the version numbers are the same, so a
/// database created under one host is understood by the other.
/// <para>
/// Steps are additive. A released version's body is never edited, because a database that already
/// recorded it will not run it again; a correction ships as a new version.
/// </para>
/// </remarks>
public interface ISchemaMigration
{
    /// <summary>
    /// Gets the name this migration's applied version is recorded under.
    /// </summary>
    /// <remarks>
    /// Part of the stored data. Renaming it makes an applied migration look unapplied, so the create
    /// step runs again against tables that already exist.
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// Creates the schema from nothing, and returns the version that leaves it at.
    /// </summary>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at.</returns>
    Task<int> CreateAsync(ISchemaBuilder builder);

    /// <summary>
    /// Moves the schema forward one step from an already-applied version.
    /// </summary>
    /// <param name="version">The version currently applied.</param>
    /// <param name="builder">The schema builder.</param>
    /// <returns>The version the schema is now at, or the same version when there is no step from it.</returns>
    Task<int> UpdateFromAsync(int version, ISchemaBuilder builder);
}
