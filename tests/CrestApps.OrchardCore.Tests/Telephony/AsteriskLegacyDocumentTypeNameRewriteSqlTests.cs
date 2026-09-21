using System.Reflection;
using CrestApps.OrchardCore.Asterisk;
using Microsoft.Data.Sqlite;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Runs the Asterisk rewrite's own predicate against a real database, over rows in the shapes a tenant holds.
/// </summary>
/// <remarks>
/// <para>
/// The Asterisk implementation moved into <c>CrestApps.Core.Telephony.Asterisk</c>, so every channel binding,
/// PJSIP credential lease and recording-ingest job already written records a type name that no longer
/// resolves. The migration exists to rewrite them; this exercises the statement it runs rather than a C#
/// re-implementation of the rules, which is what let the omnichannel rewrite skip five of the eight types it
/// claimed.
/// </para>
/// <para>
/// All three Asterisk types are stored one document per record today. The nested
/// <c>DictionaryDocument`1[[...]]</c> case is covered anyway, because the predicate claims to handle it and a
/// claim nothing exercises is the one that quietly stops being true.
/// </para>
/// </remarks>
public sealed class AsteriskLegacyDocumentTypeNameRewriteSqlTests
{
    private const string LegacyNamespace = "CrestApps.OrchardCore.Asterisk.Models.";
    private const string LegacyAssembly = "CrestApps.OrchardCore.Asterisk";
    private const string CurrentNamespace = "CrestApps.Core.Telephony.Asterisk.Models.";
    private const string CurrentAssembly = "CrestApps.Core.Telephony.Asterisk";

    /// <summary>
    /// The version segment the data layer writes into the inner name of a nested document type, and reads back
    /// when it resolves one. It is left untouched by the rewrite.
    /// </summary>
    private const string VersionSuffix = ", Version=3.0.0.0, Culture=neutral, PublicKeyToken=null";

    /// <summary>
    /// Every Asterisk type that reaches the document table.
    /// </summary>
    private static readonly string[] _storedTypes =
    [
        "AsteriskChannelTenantBinding",
        "AsteriskPjsipCredentialLease",
        "AsteriskRecordingIngestJob",
    ];

    public static TheoryData<string> StoredTypes => [.. _storedTypes];

    [Theory]
    [MemberData(nameof(StoredTypes))]
    public async Task TheRewrite_MovesATypeStoredOneDocumentPerRecord(string typeName)
    {
        // Arrange
        var stored = $"{LegacyNamespace}{typeName}, {LegacyAssembly}";

        // Act
        var rewritten = await RewriteAsync(stored);

        // Assert
        Assert.Equal($"{CurrentNamespace}{typeName}, {CurrentAssembly}", rewritten);
    }

    [Theory]
    [MemberData(nameof(StoredTypes))]
    public async Task TheRewrite_MovesATypeStoredInsideADictionaryDocument(string typeName)
    {
        // Arrange
        var stored = Nested($"{LegacyNamespace}{typeName}, {LegacyAssembly}{VersionSuffix}");

        // Act
        var rewritten = await RewriteAsync(stored);

        // Assert
        Assert.Equal(
            Nested($"{CurrentNamespace}{typeName}, {CurrentAssembly}{VersionSuffix}"),
            rewritten);
    }

    /// <summary>
    /// Pins that matching the namespace anywhere did not make the assembly match loose.
    /// </summary>
    /// <param name="stored">A recorded type the rewrite must leave exactly as it is.</param>
    [Theory]
    // A different Asterisk namespace in the same assembly: the type did move, but not from Models, so the
    // namespace replacement would produce a name that resolves to nothing.
    [InlineData("CrestApps.OrchardCore.Asterisk.Services.Something, CrestApps.OrchardCore.Asterisk")]
    // An assembly whose name merely starts with the legacy one.
    [InlineData("CrestApps.OrchardCore.Asterisk.Models.AsteriskChannelTenantBinding, CrestApps.OrchardCore.Asterisk.Extras")]
    // Already migrated: running the rewrite twice has to be a no-op.
    [InlineData("CrestApps.Core.Telephony.Asterisk.Models.AsteriskChannelTenantBinding, CrestApps.Core.Telephony.Asterisk")]
    // Another module's document entirely.
    [InlineData("CrestApps.OrchardCore.Models.DictionaryDocument`1[[CrestApps.OrchardCore.TimeZones.Models.TimeZoneMap, CrestApps.OrchardCore.TimeZones, Version=3.0.0.0, Culture=neutral, PublicKeyToken=null]], CrestApps.OrchardCore.Abstractions")]
    public async Task TheRewrite_LeavesEverythingElseExactlyAsItIs(string stored)
    {
        // Act
        var rewritten = await RewriteAsync(stored);

        // Assert
        Assert.Equal(stored, rewritten);
    }

    /// <summary>
    /// Guards the guard: if the predicate stopped selecting anything, every test above would pass while
    /// proving nothing, because a row the rewrite never touches comes back unchanged.
    /// </summary>
    [Fact]
    public async Task ThePredicate_SelectsEveryStoredTypeAndNothingElse()
    {
        // Arrange
        var moved = new List<string>();

        foreach (var typeName in _storedTypes)
        {
            moved.Add($"{LegacyNamespace}{typeName}, {LegacyAssembly}");
            moved.Add(Nested($"{LegacyNamespace}{typeName}, {LegacyAssembly}{VersionSuffix}"));
        }

        var untouched = new[]
        {
            "CrestApps.OrchardCore.Asterisk.Services.Something, CrestApps.OrchardCore.Asterisk",
            "CrestApps.OrchardCore.Asterisk.Models.AsteriskChannelTenantBinding, CrestApps.OrchardCore.Asterisk.Extras",
            "CrestApps.Core.Telephony.Asterisk.Models.AsteriskChannelTenantBinding, CrestApps.Core.Telephony.Asterisk",
        };

        await using var connection = await CreateSeededConnectionAsync([.. moved, .. untouched]);

        // Act
        await using var select = connection.CreateCommand();
        select.CommandText = $"SELECT \"Type\" FROM \"Document\" WHERE {BuildWhereClause()}";

        var selected = new List<string>();

        await using (var reader = await select.ExecuteReaderAsync(TestContext.Current.CancellationToken))
        {
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                selected.Add(reader.GetString(0));
            }
        }

        // Assert
        Assert.Equal(moved.Order(StringComparer.Ordinal), selected.Order(StringComparer.Ordinal));
    }

    private static string Nested(string inner)
        => $"CrestApps.OrchardCore.Models.DictionaryDocument`1[[{inner}]], CrestApps.OrchardCore.Abstractions";

    /// <summary>
    /// Seeds one row, runs the migration's statement over it, and reads back what it now says.
    /// </summary>
    /// <param name="stored">The recorded type name to seed.</param>
    /// <returns>The recorded type name after the rewrite.</returns>
    private static async Task<string> RewriteAsync(string stored)
    {
        await using var connection = await CreateSeededConnectionAsync([stored]);

        await using (var update = connection.CreateCommand())
        {
            update.CommandText =
                $"""
                UPDATE "Document"

                SET "Type" = REPLACE(
                REPLACE("Type", '{LegacyNamespace}', '{CurrentNamespace}'),
                ', {LegacyAssembly}',
                ', {CurrentAssembly}')

                WHERE {BuildWhereClause()}
                """;

            await update.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using var read = connection.CreateCommand();
        read.CommandText = "SELECT \"Type\" FROM \"Document\"";

        return (string)await read.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<SqliteConnection> CreateSeededConnectionAsync(IReadOnlyCollection<string> types)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using (var create = connection.CreateCommand())
        {
            create.CommandText = "CREATE TABLE \"Document\" (\"Id\" INTEGER PRIMARY KEY, \"Type\" TEXT)";
            await create.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        foreach (var type in types)
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO \"Document\" (\"Type\") VALUES ($type)";
            insert.Parameters.AddWithValue("$type", type);
            await insert.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        return connection;
    }

    /// <summary>
    /// Asks the migration itself for the predicate, so this test cannot drift away from the statement that
    /// actually runs.
    /// </summary>
    /// <returns>The SQL predicate.</returns>
    private static string BuildWhereClause()
    {
        var type = typeof(AsteriskFeatures).Assembly
            .GetType("CrestApps.OrchardCore.Asterisk.Migrations.AsteriskLegacyDocumentTypeNameMigrations", throwOnError: true);

        var method = type.GetMethod("BuildWhereClause", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        return (string)method.Invoke(null, ["\"Type\"", LegacyNamespace, LegacyAssembly]);
    }
}
