using System.Text.Json;
using CrestApps.Core.ContactCenter.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration.Records;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests.Migrations;

/// <summary>
/// Restores a tenant database written before the framework extraction and proves the current build
/// still reads it.
/// </summary>
/// <remarks>
/// Phase 1 of the extraction renames every namespace in the suite. Two things in a live tenant are
/// keyed by those names, and neither is covered by any other test here.
/// <list type="bullet">
/// <item>
/// YesSql records a document's CLR type in the <c>Type</c> column, so a renamed model stops
/// deserializing unless a migration rewrites the stored name.
/// </item>
/// <item>
/// Orchard Core records applied migrations under the migration class's full type name, so a renamed
/// migration class looks unapplied and its <c>CreateAsync</c> runs again against tables that already
/// exist. This repository has already shipped that defect once, under an earlier rename.
/// </item>
/// </list>
/// <para>
/// The snapshot is generated from a fixed commit rather than built here, because it has to be
/// written by the code as it was before anything moved. See the README beside it.
/// </para>
/// </remarks>
public sealed class PreExtractionUpgradeTests
{
    private static readonly JsonSerializerOptions _manifestOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task ATenantWrittenBeforeTheExtraction_StillLoadsEveryDocument()
    {
        // Arrange
        var manifest = ReadManifest();
        var restored = RestoreSnapshot();

        try
        {
            await using var host = await ContactCenterFeatureActivationHost.StartAsync(
                existingApplicationDataPath: restored);

            var tenant = await host.GetExistingTenantAsync(manifest.TenantName);

            // Act: entering the tenant scope activates the shell, which runs today's migrations
            // against the restored database.
            await host.ActivateTenantAsync(tenant);

            var loaded = new Dictionary<string, int>(StringComparer.Ordinal);

            await host.ExecuteInTenantScopeAsync(tenant, async serviceProvider =>
            {
                var session = serviceProvider.GetRequiredService<ISession>();
                var collection = manifest.CollectionName;

                // Keyed by the name the snapshot recorded, not the name the type has now. The manifest is a
                // record of what a tenant wrote before the extraction, so it speaks in the old names; the
                // point of this gate is that those rows come back, whatever the type is called today.
                loaded[LegacyNameOf(nameof(ActivityQueue))] = await CountAsync<ActivityQueue>(session, collection);
                loaded[LegacyNameOf(nameof(AgentProfile))] = await CountAsync<AgentProfile>(session, collection);
                loaded[LegacyNameOf(nameof(AgentSession))] = await CountAsync<AgentSession>(session, collection);
                loaded[LegacyNameOf(nameof(QueueItem))] = await CountAsync<QueueItem>(session, collection);
            });

            // Assert
            var lost = manifest.DocumentCounts
                .Where(recorded => !loaded.TryGetValue(recorded.Key, out var now) || now < recorded.Value)
                .Select(recorded =>
                    $"{recorded.Key}: {recorded.Value} stored, " +
                    $"{(loaded.TryGetValue(recorded.Key, out var now) ? now.ToString() : "type no longer queried")} read back")
                .ToArray();

            Assert.True(
                lost.Length == 0,
                "Documents written before the extraction no longer load. On a live tenant this is data loss, not a " +
                "test failure: a renamed model needs a migration that rewrites the stored type name." +
                Environment.NewLine + Environment.NewLine +
                string.Join(Environment.NewLine, lost.Select(entry => "  - " + entry)));

            Assert.True(
                loaded.Values.Sum() > 0,
                "Nothing at all was read from the restored tenant, so this gate proves nothing.");
        }
        finally
        {
            TryDelete(restored);
        }
    }

    /// <summary>
    /// The full type name the snapshot recorded for a Contact Center model.
    /// </summary>
    /// <remarks>
    /// Every one of these lived in <c>CrestApps.OrchardCore.ContactCenter.Core.Models</c> when the snapshot
    /// was taken and lives in <c>CrestApps.Core.ContactCenter.Models</c> now. Spelling the old namespace out
    /// here rather than deriving it keeps this gate honest: it asserts against what was actually written,
    /// not against whatever the code currently calls itself.
    /// </remarks>
    /// <param name="typeName">The simple type name.</param>
    /// <returns>The full type name as the snapshot recorded it.</returns>
    private static string LegacyNameOf(string typeName)
        => "CrestApps.OrchardCore.ContactCenter.Core.Models." + typeName;

    [Fact]
    public async Task ATenantWrittenBeforeTheExtraction_DoesNotRerunItsMigrations()
    {
        // Arrange
        var manifest = ReadManifest();
        var restored = RestoreSnapshot();

        try
        {
            await using var host = await ContactCenterFeatureActivationHost.StartAsync(
                existingApplicationDataPath: restored);

            var tenant = await host.GetExistingTenantAsync(manifest.TenantName);

            // Act
            await host.ActivateTenantAsync(tenant);

            var applied = new Dictionary<string, int>(StringComparer.Ordinal);

            await host.ExecuteInTenantScopeAsync(tenant, async serviceProvider =>
            {
                var session = serviceProvider.GetRequiredService<ISession>();
                var record = await session.Query<DataMigrationRecord>().FirstOrDefaultAsync();

                foreach (var migration in record?.DataMigrations ?? [])
                {
                    applied[migration.DataMigrationClass] = migration.Version ?? 0;
                }
            });

            // Assert
            Assert.NotEmpty(applied);

            var regressed = manifest.MigrationVersions
                .Where(recorded => !applied.TryGetValue(recorded.Key, out var now) || now < recorded.Value)
                .Select(recorded =>
                    $"{recorded.Key}: was version {recorded.Value}, " +
                    $"{(applied.TryGetValue(recorded.Key, out var now) ? $"now {now}" : "no longer recorded")}")
                .ToArray();

            Assert.True(
                regressed.Length == 0,
                "These migrations were applied in the snapshot and the current build does not see them as applied. " +
                "Orchard Core keys applied migrations by the migration class's full type name, so a renamed " +
                "migration class runs its CreateAsync again against tables that already exist." +
                Environment.NewLine + Environment.NewLine +
                string.Join(Environment.NewLine, regressed.Select(entry => "  - " + entry)));
        }
        finally
        {
            TryDelete(restored);
        }
    }

    /// <summary>
    /// Counts the documents of a type in a collection.
    /// </summary>
    /// <typeparam name="T">The document type.</typeparam>
    /// <param name="session">The session.</param>
    /// <param name="collection">The collection name.</param>
    /// <returns>The document count.</returns>
    private static async Task<int> CountAsync<T>(ISession session, string collection)
        where T : class
        => (int)await session.Query<T>(collection: collection).CountAsync();

    /// <summary>
    /// Copies the checked-in snapshot into a temporary application data directory.
    /// </summary>
    /// <returns>The temporary directory.</returns>
    private static string RestoreSnapshot()
    {
        var source = GetSnapshotDirectory();
        var target = Path.Combine(Path.GetTempPath(), $"crestapps-upgrade-{Guid.NewGuid():N}");

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var extension = Path.GetExtension(file);

            // The manifest and the regeneration notes describe the snapshot; they are not part of it.
            if (string.Equals(Path.GetFileName(file), "manifest.json", StringComparison.Ordinal) ||
                string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(file, destination);
        }

        Directory.CreateDirectory(Path.Combine(target, "wwwroot"));

        return target;
    }

    /// <summary>
    /// Removes the restored copy, tolerating a database handle that outlives the host.
    /// </summary>
    /// <param name="path">The directory to remove.</param>
    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Microsoft.Data.Sqlite pools connections, and a pooled handle can keep the file open on
            // Windows. A leftover temporary directory is not worth failing a passing test over.
        }
    }

    /// <summary>
    /// Reads the manifest that records what the snapshot contains.
    /// </summary>
    /// <returns>The manifest.</returns>
    private static SnapshotManifest ReadManifest()
    {
        var path = Path.Combine(GetSnapshotDirectory(), "manifest.json");

        Assert.True(File.Exists(path), $"The pre-extraction snapshot manifest is missing from '{path}'.");

        var manifest = JsonSerializer.Deserialize<SnapshotManifest>(File.ReadAllText(path), _manifestOptions);

        Assert.True(manifest?.DocumentCounts?.Count > 0, "The snapshot manifest records no documents.");

        return manifest;
    }

    /// <summary>
    /// Gets the snapshot directory from the repository rather than the test output folder.
    /// </summary>
    /// <returns>The snapshot directory.</returns>
    private static string GetSnapshotDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.True(directory is not null, "The repository root could not be located from the test output directory.");

        return Path.Combine(
            directory.FullName,
            "tests",
            "CrestApps.OrchardCore.ContactCenter.FeatureActivationTests",
            "Migrations",
            "Snapshots",
            "pre-extraction");
    }

    private sealed class SnapshotManifest
    {
        public string SourceCommit { get; set; }

        public string TenantName { get; set; }

        public string TablePrefix { get; set; }

        public string CollectionName { get; set; }

        public Dictionary<string, int> DocumentCounts { get; set; }

        public Dictionary<string, int> MigrationVersions { get; set; } = [];
    }
}
