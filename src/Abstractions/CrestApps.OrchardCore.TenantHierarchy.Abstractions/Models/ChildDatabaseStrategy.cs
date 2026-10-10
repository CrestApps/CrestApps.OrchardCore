namespace CrestApps.OrchardCore.TenantHierarchy.Models;

/// <summary>
/// Describes where the database of a new child tenant is placed.
/// </summary>
public enum ChildDatabaseStrategy
{
    /// <summary>
    /// Every child gets its own SQLite database file. Use it for development and small installations.
    /// </summary>
    SqlitePerChild,

    /// <summary>
    /// Every child gets its own database and a login that can reach only that database. It is the strongest isolation.
    /// </summary>
    DatabasePerChild,

    /// <summary>
    /// Every child gets its own schema in the database of a pool.
    /// </summary>
    SchemaPerChild,

    /// <summary>
    /// Every child gets its own table prefix in the database of a pool. The SQL queries feature is blocked in children.
    /// </summary>
    TablePrefixPerChild,
}
