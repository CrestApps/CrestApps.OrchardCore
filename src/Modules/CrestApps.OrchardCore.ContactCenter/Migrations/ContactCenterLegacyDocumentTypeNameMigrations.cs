using System.Data.Common;
using CrestApps.Core.ContactCenter;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Rewrites the stored type name of every Contact Center document written before the models moved into the
/// framework assemblies.
/// </summary>
/// <remarks>
/// <para>
/// YesSql records the CLR type of a document as <c>Namespace.TypeName, AssemblyName</c> and resolves it on
/// read, so moving a type makes every document already written unreadable. For a tenant that is its queues,
/// its agents and their sessions, every interaction and the events recorded against them, the outbox, the
/// metrics and their projection checkpoints - the whole floor.
/// </para>
/// <para>
/// Twenty-five stored types moved, and they all came from one namespace in one assembly and landed in one
/// namespace in one assembly, so one rule covers them. The assembly is matched as an exact suffix rather than
/// by prefix, because <c>CrestApps.OrchardCore.ContactCenter</c> is a prefix of
/// <c>CrestApps.OrchardCore.ContactCenter.Core</c> and a loose match would rewrite a module document to a
/// namespace it was never in.
/// </para>
/// </remarks>
internal sealed class ContactCenterLegacyDocumentTypeNameMigrations : DataMigration
{
    /// <summary>
    /// The collections Contact Center documents were written into.
    /// </summary>
    private static readonly string[] _collections = [ContactCenterStorage.CollectionName, string.Empty];

    /// <summary>
    /// The replacements to apply, as legacy namespace prefix, legacy assembly, current namespace
    /// prefix and current assembly.
    /// </summary>
    private static readonly (string LegacyNamespacePrefix, string LegacyAssemblyName, string CurrentNamespacePrefix, string CurrentAssemblyName)[] _legacyTypeNameReplacements =
    [
        (
            "CrestApps.OrchardCore.ContactCenter.Core.Models.",
            "CrestApps.OrchardCore.ContactCenter.Core",
            "CrestApps.Core.ContactCenter.Models.",
            "CrestApps.Core.ContactCenter.Abstractions"
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
        var logger = scope.GetRequiredService<ILogger<ContactCenterLegacyDocumentTypeNameMigrations>>();

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
                "Updated {Count} legacy Contact Center document type names to the current CrestApps.Core assemblies.",
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
