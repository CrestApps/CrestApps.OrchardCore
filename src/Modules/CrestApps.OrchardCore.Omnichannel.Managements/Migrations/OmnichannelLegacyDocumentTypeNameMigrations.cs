using System.Data.Common;
using CrestApps.Core.Omnichannel;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Migrations;

/// <summary>
/// Rewrites the stored type name of every omnichannel document that was written before the omnichannel models
/// moved into the framework assemblies.
/// </summary>
/// <remarks>
/// <para>
/// YesSql records the CLR type of a document as <c>Namespace.TypeName, AssemblyName</c> and resolves it on
/// read. Moving a type to a different namespace or assembly therefore makes every document already written
/// unreadable. For a tenant that means its activities, the batches that loaded them, its campaigns,
/// dispositions, cadences, subject actions and channel endpoints all silently disappear - the whole CRM side
/// of the product.
/// </para>
/// <para>
/// Eight stored types moved: <c>OmnichannelActivity</c>, <c>OmnichannelActivityBatch</c>,
/// <c>OmnichannelCampaign</c>, <c>OmnichannelCampaignGroup</c>, <c>OmnichannelDisposition</c>,
/// <c>OmnichannelChannelEndpoint</c>, <c>SubjectAction</c> and <c>Cadence</c>. They came from one namespace in
/// one assembly and landed in one namespace in one assembly, so a single replacement rule covers them.
/// </para>
/// <para>
/// They are not all stored the same way, and that is what the first version of this migration got wrong. A
/// store that writes one document per record - <c>OmnichannelActivity</c>, <c>OmnichannelActivityBatch</c> and
/// <c>Cadence</c> - records the type as <c>Namespace.TypeName, Assembly</c>. A catalog backed by
/// <c>IDocumentManager</c> keeps the whole set in one document, and records the nested type of that wrapper:
/// <c>CrestApps.OrchardCore.Models.DictionaryDocument`1[[Namespace.TypeName, Assembly, Version=..., ...]],
/// CrestApps.OrchardCore.Abstractions</c>. The other five types are stored that way, either through an
/// explicit <c>Catalog&lt;T&gt;</c> or through the open-generic registration.
/// </para>
/// <para>
/// The replacement itself works on both shapes, because it rewrites substrings. Only the match had to change:
/// it anchored the namespace to the start of the value and the assembly to the end, which is true of the flat
/// shape and of neither nested one. The inner name's <c>Version=</c> segment is deliberately left alone - the
/// data layer computes the same segment when it reads, and every assembly in this repository carries the one
/// version set by <c>VersionPrefix</c>, so the value that comes out is the value a read looks for.
/// </para>
/// <para>
/// The assembly is matched exactly rather than by prefix, for the reason the telephony rewrite records: the
/// legacy assembly names are prefixes of one another, so a loose match would rewrite a document to the wrong
/// assembly and lose it just as surely as not rewriting it at all.
/// </para>
/// </remarks>
internal sealed class OmnichannelLegacyDocumentTypeNameMigrations : DataMigration
{
    /// <summary>
    /// The collections omnichannel documents were written into.
    /// </summary>
    private static readonly string[] _collections = [OmnichannelCollections.Name, string.Empty];

    /// <summary>
    /// The replacements to apply, as legacy namespace prefix, legacy assembly, current namespace
    /// prefix and current assembly.
    /// </summary>
    private static readonly (string LegacyNamespacePrefix, string LegacyAssemblyName, string CurrentNamespacePrefix, string CurrentAssemblyName)[] _legacyTypeNameReplacements =
    [
        (
            "CrestApps.OrchardCore.Omnichannel.Core.Models.",
            "CrestApps.OrchardCore.Omnichannel.Core",
            "CrestApps.Core.Omnichannel.Models.",
            "CrestApps.Core.Omnichannel.Abstractions"
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
        var logger = scope.GetRequiredService<ILogger<OmnichannelLegacyDocumentTypeNameMigrations>>();

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
                "Updated {Count} legacy omnichannel document type names to the current CrestApps.Core assemblies.",
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
    /// The namespace is matched anywhere rather than at the start, because in a nested
    /// <c>DictionaryDocument`1[[...]]</c> value it is not at the start. The assembly is still matched exactly:
    /// it must be followed by the end of the value or by a comma, so an assembly whose name merely starts with
    /// this one is not rewritten into a type that never existed.
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
