using System.Data.Common;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using YesSql;

namespace CrestApps.OrchardCore.Telephony.Migrations;

/// <summary>
/// Rewrites the stored type name of every telephony document that was written before the telephony code moved
/// into the framework assemblies.
/// </summary>
/// <remarks>
/// YesSql records the CLR type of a document as <c>Namespace.TypeName, AssemblyName</c> and resolves it on
/// read. Moving a type to a different namespace or assembly therefore makes every document already written
/// unreadable, which for a tenant means its call history and its extension directory silently disappear. This
/// rewrites the recorded names in place, once, so the same rows resolve to the types that now exist.
/// </remarks>
internal sealed class TelephonyLegacyDocumentTypeNameMigrations : DataMigration
{
    /// <summary>
    /// The replacements to apply, as legacy namespace prefix, legacy assembly, current namespace prefix and
    /// current assembly.
    /// </summary>
    /// <remarks>
    /// The assembly is matched exactly rather than by prefix, because the legacy assembly names are prefixes
    /// of one another: matching <c>CrestApps.OrchardCore.Telephony</c> loosely would also rewrite the rows
    /// written by <c>CrestApps.OrchardCore.Telephony.Abstractions</c>, to the wrong assembly.
    /// <para>
    /// <c>TelephonyUserConnections</c> is not here. It is stored in a user's properties under its simple type
    /// name rather than as a document, so its namespace never reached the database.
    /// </para>
    /// </remarks>
    /// <summary>
    /// The collections telephony documents were written into.
    /// </summary>
    private static readonly string[] _collections = [string.Empty];

    /// <summary>
    /// The replacements to apply, as legacy namespace prefix, legacy assembly, current namespace
    /// prefix and current assembly.
    /// </summary>
    private static readonly (string LegacyNamespacePrefix, string LegacyAssemblyName, string CurrentNamespacePrefix, string CurrentAssemblyName)[] _legacyTypeNameReplacements =
    [
        // TelephonyInteraction and the other contract models.
        (
            "CrestApps.OrchardCore.Telephony.Models.",
            "CrestApps.OrchardCore.Telephony.Abstractions",
            "CrestApps.Core.Telephony.Models.",
            "CrestApps.Core.Telephony.Abstractions"
        ),

        // TelephonyExtension, which lived in the Orchard Telephony.Core project that no longer exists.
        (
            "CrestApps.OrchardCore.Telephony.Core.Models.",
            "CrestApps.OrchardCore.Telephony.Core",
            "CrestApps.Core.Telephony.Models.",
            "CrestApps.Core.Telephony"
        ),
    ];

    /// <summary>
    /// Rewrites the legacy type names.
    /// </summary>
    /// <returns>The schema version this migration leaves behind.</returns>
    public static int Create()
    {
        ShellScope.AddDeferredTask(scope => RewriteLegacyTypeNamesAsync(scope.ServiceProvider));

        return 1;
    }

    private static async Task RewriteLegacyTypeNamesAsync(IServiceProvider scope)
    {
        var store = scope.GetRequiredService<IStore>();
        var dbConnectionAccessor = scope.GetRequiredService<IDbConnectionAccessor>();
        var logger = scope.GetRequiredService<ILogger<TelephonyLegacyDocumentTypeNameMigrations>>();

        var dialect = store.Configuration.SqlDialect;
        var quotedTypeColumnName = dialect.QuoteForColumnName(nameof(Document.Type));

        await using var connection = dbConnectionAccessor.CreateConnection();
        await connection.OpenAsync();

        var totalUpdated = 0;

        // Every collection, not only the default one. A document written into a named collection lives in
        // that collection's own table, so a rewrite that reads GetDocumentTable() with no argument silently
        // leaves exactly the rows this migration exists for.
        foreach (var collection in _collections)
        {
            var documentTableName = store.Configuration.TableNameConvention.GetDocumentTable(collection);
            var table = $"{store.Configuration.TablePrefix}{documentTableName}";
            var quotedTableName = dialect.QuoteForTableName(table, store.Configuration.Schema);

            if (!await TableExistsAsync(connection, quotedTableName))
            {
                continue;
            }

            foreach (var (legacyNamespacePrefix, legacyAssemblyName, currentNamespacePrefix, currentAssemblyName) in _legacyTypeNameReplacements)
            {
                var whereClause = BuildWhereClause(quotedTypeColumnName, legacyNamespacePrefix, legacyAssemblyName);

                var count = await connection.ExecuteScalarAsync<int>(
                    $"SELECT COUNT(*) FROM {quotedTableName} WHERE {whereClause}");

                if (count == 0)
                {
                    continue;
                }

                totalUpdated += await connection.ExecuteAsync(
                    $"""
                    UPDATE {quotedTableName}

                    SET {quotedTypeColumnName} = REPLACE(
                    REPLACE({quotedTypeColumnName}, '{legacyNamespacePrefix}', '{currentNamespacePrefix}'),
                    ', {legacyAssemblyName}',
                    ', {currentAssemblyName}')

                    WHERE {whereClause}
                    """);
            }
        }

        if (totalUpdated > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Updated {Count} legacy telephony document type names to the current CrestApps.Core assemblies.",
                totalUpdated);
        }
    }

    /// <summary>
    /// Whether a collection's document table is present.
    /// </summary>
    /// <remarks>
    /// A tenant that never used a collection has no table for it, and asking is cheaper than deciding which
    /// collections a given tenant happens to have enabled.
    /// </remarks>
    /// <param name="connection">The open connection.</param>
    /// <param name="quotedTableName">The quoted table name.</param>
    /// <returns><see langword="true"/> when the table exists.</returns>
    private static async Task<bool> TableExistsAsync(DbConnection connection, string quotedTableName)
    {
        try
        {
            await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {quotedTableName} WHERE 1 = 0");

            return true;
        }
        catch (DbException)
        {
            return false;
        }
    }

    /// <summary>
    /// Builds the match for one replacement rule, covering both shapes a moved type can be stored in.
    /// </summary>
    /// <remarks>
    /// Every store behind this module writes one document per record, so today only the flat
    /// <c>Namespace.TypeName, Assembly</c> shape occurs. The nested <c>DictionaryDocument`1[[...]]</c> shape is
    /// matched anyway: a type that later moves to a catalog-backed store would otherwise be skipped silently,
    /// which is exactly how the omnichannel rewrite came to miss five of the eight types it claimed.
    /// </remarks>
    /// <param name="quotedTypeColumnName">The quoted type column.</param>
    /// <param name="legacyNamespacePrefix">The legacy namespace prefix.</param>
    /// <param name="legacyAssemblyName">The legacy assembly name.</param>
    /// <returns>The SQL predicate.</returns>
    private static string BuildWhereClause(string quotedTypeColumnName, string legacyNamespacePrefix, string legacyAssemblyName)
        => $"{quotedTypeColumnName} LIKE '%{legacyNamespacePrefix}%' AND (" +
           $"{quotedTypeColumnName} LIKE '%, {legacyAssemblyName}' OR " +
           $"{quotedTypeColumnName} LIKE '%, {legacyAssemblyName},%')";

    /// <summary>
    /// Applies the same replacements the SQL above applies, to one recorded type name.
    /// </summary>
    /// <remarks>
    /// This exists so the rules can be exercised without a database. The rules themselves are the shared
    /// table, so a test against this is a test of what the update statement does.
    /// </remarks>
    /// <param name="typeName">The recorded type name.</param>
    /// <returns>The rewritten type name, or the original when no rule matches it.</returns>
    private static string RewriteLegacyTypeName(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return typeName;
        }

        foreach (var (legacyNamespacePrefix, legacyAssemblyName, currentNamespacePrefix, currentAssemblyName) in _legacyTypeNameReplacements)
        {
            if (!typeName.Contains(legacyNamespacePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            // The assembly still has to match exactly, so it must be followed by the end of the value (the flat
            // shape) or by a comma (the nested shape, where the version segment follows). Matching it loosely
            // would rewrite a document belonging to an assembly whose name merely starts with this one, and the
            // legacy assembly names are prefixes of one another.
            var assemblySuffix = ", " + legacyAssemblyName;

            if (!typeName.EndsWith(assemblySuffix, StringComparison.Ordinal) &&
                !typeName.Contains(assemblySuffix + ",", StringComparison.Ordinal))
            {
                continue;
            }

            return typeName
                .Replace(legacyNamespacePrefix, currentNamespacePrefix, StringComparison.Ordinal)
                .Replace(assemblySuffix, ", " + currentAssemblyName, StringComparison.Ordinal);
        }

        return typeName;
    }
}
