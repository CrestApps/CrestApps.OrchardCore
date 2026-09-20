using System.Reflection;
using CrestApps.OrchardCore.Omnichannel.Managements.Migrations;
using Microsoft.Data.Sqlite;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Migrations;

/// <summary>
/// Runs the rewrite's own predicate against a real database, over rows in the shapes a tenant actually holds.
/// </summary>
/// <remarks>
/// <para>
/// The first version of this rewrite matched the namespace at the start of the recorded type and the assembly
/// at the end. That is true of a type stored one document per record, and false of a type stored through a
/// catalog, whose document records the nested <c>DictionaryDocument`1[[...]]</c> wrapper. Five of the eight
/// types the migration claims to cover are stored the second way, so it silently skipped them and their
/// records would have disappeared on upgrade.
/// </para>
/// <para>
/// Nothing caught it, because the only test exercised a C# mirror of the rules over flat names. This one
/// seeds both shapes and executes the predicate in SQLite, so the thing under test is the statement the
/// migration runs rather than a re-implementation of it.
/// </para>
/// </remarks>
public sealed class LegacyDocumentTypeNameRewriteSqlTests
{
    private const string LegacyNamespace = "CrestApps.OrchardCore.Omnichannel.Core.Models.";
    private const string LegacyAssembly = "CrestApps.OrchardCore.Omnichannel.Core";
    private const string CurrentNamespace = "CrestApps.Core.Omnichannel.Models.";
    private const string CurrentAssembly = "CrestApps.Core.Omnichannel.Abstractions";

    /// <summary>
    /// The version segment the data layer writes into the inner name of a nested document type, and reads back
    /// when it resolves one. It is left untouched by the rewrite.
    /// </summary>
    private const string VersionSuffix = ", Version=3.0.0.0, Culture=neutral, PublicKeyToken=null";

    /// <summary>
    /// The types stored one document per record.
    /// </summary>
    private static readonly string[] _flatTypes =
    [
        "OmnichannelActivity",
        "OmnichannelActivityBatch",
        "Cadence",
    ];

    /// <summary>
    /// The types stored through a catalog, whose document records the wrapper rather than the type itself.
    /// </summary>
    private static readonly string[] _catalogTypes =
    [
        "OmnichannelCampaign",
        "OmnichannelCampaignGroup",
        "OmnichannelDisposition",
        "OmnichannelChannelEndpoint",
        "SubjectAction",
    ];

    public static TheoryData<string> FlatTypes => [.. _flatTypes];

    public static TheoryData<string> CatalogTypes => [.. _catalogTypes];

    [Theory]
    [MemberData(nameof(FlatTypes))]
    public async Task TheRewrite_MovesATypeStoredOneDocumentPerRecord(string typeName)
    {
        // Arrange
        var stored = $"{LegacyNamespace}{typeName}, {LegacyAssembly}";

        // Act
        var rewritten = await RewriteAsync(stored);

        // Assert
        Assert.Equal($"{CurrentNamespace}{typeName}, {CurrentAssembly}", rewritten);
    }

    /// <summary>
    /// The case the original rewrite missed entirely.
    /// </summary>
    /// <param name="typeName">The moved type held inside the wrapper.</param>
    [Theory]
    [MemberData(nameof(CatalogTypes))]
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
    /// Pins that widening the match did not make it loose.
    /// </summary>
    /// <remarks>
    /// The legacy assembly names are prefixes of one another, so an assembly is a match only when the name is
    /// followed by the end of the value or by a comma. A row rewritten to an assembly it was never in is lost
    /// just as completely as a row that was skipped.
    /// </remarks>
    /// <param name="stored">A recorded type the rewrite must leave exactly as it is.</param>
    [Theory]
    [InlineData("CrestApps.OrchardCore.Omnichannel.Models.OmnichannelMessage, CrestApps.OrchardCore.Omnichannel")]
    [InlineData("CrestApps.OrchardCore.Omnichannel.Core.Models.OmnichannelActivity, CrestApps.OrchardCore.Omnichannel.Core.Extras")]
    [InlineData("CrestApps.Core.Omnichannel.Models.OmnichannelActivity, CrestApps.Core.Omnichannel.Abstractions")]
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
    public async Task ThePredicate_SelectsEveryMovedTypeAndNothingElse()
    {
        // Arrange
        var moved = new List<string>();

        foreach (var typeName in _flatTypes)
        {
            moved.Add($"{LegacyNamespace}{typeName}, {LegacyAssembly}");
        }

        foreach (var typeName in _catalogTypes)
        {
            moved.Add(Nested($"{LegacyNamespace}{typeName}, {LegacyAssembly}{VersionSuffix}"));
        }

        var untouched = new[]
        {
            "CrestApps.OrchardCore.Omnichannel.Models.OmnichannelMessage, CrestApps.OrchardCore.Omnichannel",
            "CrestApps.OrchardCore.Omnichannel.Core.Models.OmnichannelActivity, CrestApps.OrchardCore.Omnichannel.Core.Extras",
            "OrchardCore.Users.Models.User, OrchardCore.Users.Core",
        };

        await using var connection = await CreateSeededConnectionAsync([.. moved, .. untouched]);

        // Act
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT \"Type\" FROM \"Document\" WHERE {BuildWhereClause()}";

        var selected = new List<string>();

        await using (var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken))
        {
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                selected.Add(reader.GetString(0));
            }
        }

        // Assert
        Assert.Equal(8, moved.Count);
        Assert.Equal([.. moved.Order(StringComparer.Ordinal)], [.. selected.Order(StringComparer.Ordinal)]);
    }

    private static string Nested(string inner)
        => $"CrestApps.OrchardCore.Models.DictionaryDocument`1[[{inner}]], CrestApps.OrchardCore.Abstractions";

    /// <summary>
    /// Runs the migration's predicate and replacement over one row and returns what the row holds afterwards.
    /// </summary>
    /// <param name="stored">The recorded type to seed.</param>
    /// <returns>The recorded type after the rewrite.</returns>
    private static async Task<string> RewriteAsync(string stored)
    {
        await using var connection = await CreateSeededConnectionAsync([stored]);

        await using (var update = connection.CreateCommand())
        {
            // The same two replacements the migration applies, over the predicate it builds.
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
        var type = typeof(OmnichannelActivityBatchIndexMigrations).Assembly
            .GetType("CrestApps.OrchardCore.Omnichannel.Managements.Migrations.OmnichannelLegacyDocumentTypeNameMigrations", throwOnError: true);

        var method = type.GetMethod("BuildWhereClause", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        return (string)method.Invoke(null, ["\"Type\"", LegacyNamespace, LegacyAssembly]);
    }
}
