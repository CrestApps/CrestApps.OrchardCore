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
/// one assembly and landed in one namespace in one assembly, so a single rule covers them.
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
    /// The replacements to apply, as legacy namespace prefix, legacy assembly, current namespace prefix and
    /// current assembly.
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
        var documentTableName = store.Configuration.TableNameConvention.GetDocumentTable();
        var table = $"{store.Configuration.TablePrefix}{documentTableName}";
        var quotedTableName = dialect.QuoteForTableName(table, store.Configuration.Schema);
        var quotedTypeColumnName = dialect.QuoteForColumnName(nameof(Document.Type));

        await using var connection = dbConnectionAccessor.CreateConnection();
        await connection.OpenAsync();

        var totalUpdated = 0;

        foreach (var (legacyNamespacePrefix, legacyAssemblyName, currentNamespacePrefix, currentAssemblyName) in _legacyTypeNameReplacements)
        {
            var whereClause =
                $"{quotedTypeColumnName} LIKE '{legacyNamespacePrefix}%' AND {quotedTypeColumnName} LIKE '%, {legacyAssemblyName}'";

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

        if (totalUpdated > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Updated {Count} legacy omnichannel document type names in {TableName} to the current CrestApps.Core assemblies.",
                totalUpdated,
                table);
        }
    }

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
            if (!typeName.StartsWith(legacyNamespacePrefix, StringComparison.Ordinal) ||
                !typeName.EndsWith(", " + legacyAssemblyName, StringComparison.Ordinal))
            {
                continue;
            }

            return typeName
                .Replace(legacyNamespacePrefix, currentNamespacePrefix, StringComparison.Ordinal)
                .Replace(", " + legacyAssemblyName, ", " + currentAssemblyName, StringComparison.Ordinal);
        }

        return typeName;
    }
}
