using System.Collections.Concurrent;
using System.Data.Common;
using System.Reflection;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Recognizes a database error that only says another writer held the database at that moment, so the same work can
/// succeed if it is tried again shortly.
/// </summary>
/// <remarks>
/// A provider that marks such errors transient (<see cref="DbException.IsTransient"/>) is taken at its word. SQLite
/// does not: a writer it could not wait out surfaces as <c>SQLITE_BUSY</c> (5) or <c>SQLITE_LOCKED</c> (6) with
/// <see cref="DbException.IsTransient"/> left false, so those two codes are read from the provider's exception. The
/// provider is not referenced for this, which keeps the module free of any one database provider.
/// </remarks>
internal static class TransientDatabaseErrors
{
    private const int SqliteBusy = 5;
    private const int SqliteLocked = 6;

    private static readonly ConcurrentDictionary<Type, PropertyInfo> _sqliteErrorCodeProperties = new();

    /// <summary>
    /// Determines whether the exception, or one it wraps, is a database reporting that it was busy.
    /// </summary>
    /// <param name="exception">The exception to inspect.</param>
    /// <returns><see langword="true"/> when trying the work again may succeed; otherwise <see langword="false"/>.</returns>
    public static bool IsTransient(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is not DbException databaseException)
            {
                continue;
            }

            if (databaseException.IsTransient)
            {
                return true;
            }

            if (TryGetSqliteErrorCode(databaseException, out var errorCode) &&
                errorCode is SqliteBusy or SqliteLocked)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetSqliteErrorCode(DbException exception, out int errorCode)
    {
        errorCode = 0;

        var property = _sqliteErrorCodeProperties.GetOrAdd(
            exception.GetType(),
            static type => type.GetProperty("SqliteErrorCode", BindingFlags.Public | BindingFlags.Instance) is { PropertyType: var propertyType } candidate &&
                propertyType == typeof(int)
                    ? candidate
                    : null);

        if (property?.GetValue(exception) is not int value)
        {
            return false;
        }

        errorCode = value;

        return true;
    }
}
