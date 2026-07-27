using System.Data.Common;
using System.Globalization;
using System.Reflection;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Migrations;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Tests.Modules.ContactCenter.RollingUpgrade;
using Moq;
using OrchardCore.Data.Migration;
using OrchardCore.Recipes.Services;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Proves a node running the previous version stays healthy against the database a node running the current
/// version has already upgraded, which is what makes the zero-downtime rolling upgrade in the deployment
/// documentation true rather than aspirational.
/// </summary>
/// <remarks>
/// The additive-migration gate reads migrations statically and rejects a destructive operation. That is
/// necessary but not sufficient: adding a non-nullable column with no default is entirely additive, passes
/// that gate, and still breaks every write the still-running previous version performs, because that version
/// supplies no value for a column it does not know about. Only executing both write shapes against one real
/// upgraded database can catch it.
/// <para>
/// The previous version's schema is reached two ways, depending on how the migration is versioned. When the
/// create step reports a version older than an update step, a fresh installation runs that step too, so the
/// previous version is simply the same chain stopped one step short — no synthesis at all. When the create
/// step already reports the current version, the older create step is no longer in the tree, and the update
/// step is the only surviving description of what that version lacked, so the older table is the current one
/// minus the columns that step adds. That is not an approximation: it is the contract Orchard's migration
/// versioning relies on, since an <c>UpdateFromNAsync</c> step exists precisely because the create step once
/// produced the smaller table.
/// </para>
/// <para>
/// One rolling-upgrade hazard is deliberately out of scope because no portable fix exists: an upgrade step
/// that adds a non-nullable column with a shared default and then places a unique constraint on it admits
/// exactly one previous-version write, because every previous-version node writes the same default. Filtered
/// indexes are not portable across the supported engines, so the safe pattern is to expand and contract over
/// two releases. This is documented as an operator constraint rather than enforced here.
/// </para>
/// </remarks>
public sealed class ContactCenterRollingUpgradeTests
{
    private const string CollectionName = ContactCenterConstants.CollectionName;

    // Discovery floors. These stop the suite passing vacuously if migration discovery silently breaks, which
    // would otherwise turn every assertion below into a loop over an empty set.
    private const int MinimumMigrationCount = 20;
    private const int MinimumUpgradeStepCount = 8;

    [Fact]
    public async Task MigrationDiscovery_FindsEveryMigrationAndUpgradeStep_SoTheSuiteCannotPassVacuously()
    {
        await using var context = await RollingUpgradeContext.CreateAsync();

        Assert.True(
            context.Migrations.Count >= MinimumMigrationCount,
            $"Expected at least {MinimumMigrationCount} Contact Center migrations but discovered {context.Migrations.Count}. Migration discovery has broken, which would make every rolling-upgrade assertion vacuous.");

        var upgradeSteps = context.Migrations.Sum(migration => migration.UpgradeSteps.Count);

        Assert.True(
            upgradeSteps >= MinimumUpgradeStepCount,
            $"Expected at least {MinimumUpgradeStepCount} upgrade steps but discovered {upgradeSteps}.");
    }

    [Fact]
    public async Task PreviousVersionSchema_IsAStrictSubsetForAtLeastOneTable_SoTheComparisonIsMeaningful()
    {
        await using var context = await RollingUpgradeContext.CreateAsync();
        await context.ApplyFreshInstallAsync();
        await context.CaptureUpgradeStepsAsync();

        var narrowed = context.Tables.Values
            .Where(table => table.AddedColumns.Count > 0)
            .ToList();

        Assert.True(
            narrowed.Count > 0,
            "No upgrade step was observed to add a column, so the previous-version schema is identical to the current one and the rolling-upgrade assertions would be trivially true.");
    }

    [Fact]
    public async Task PreviousVersionWriter_CanStillInsert_AfterTheCurrentVersionUpgradedTheDatabase()
    {
        await using var fresh = await RollingUpgradeContext.CreateAsync();
        await fresh.ApplyFreshInstallAsync();
        await fresh.CaptureUpgradeStepsAsync();

        // The write has to be exercised against a database that was upgraded, not one that was installed
        // fresh, because that is the only state a rolling upgrade actually produces.
        await using var upgraded = await RollingUpgradeContext.CreateAsync();
        await upgraded.ApplyPreviousVersionInstallAsync(fresh);
        await upgraded.ApplyUpgradeStepsAsync();

        var failures = new List<string>();
        var exercised = 0;

        foreach (var table in fresh.Tables.Values.Where(candidate => candidate.AddedColumns.Count > 0))
        {
            var addedNames = table.AddedColumns
                .Select(column => column.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var previousVersionColumns = table.CreatedColumns
                .Where(column => !addedNames.Contains(column.Name))
                .ToList();

            var currentVersionColumns = previousVersionColumns
                .Concat(table.AddedColumns)
                .ToList();

            exercised++;

            // The current version writes every column it knows about.
            var currentVersionError = await upgraded.TryInsertAsync(table.TableName, currentVersionColumns, documentId: 1);

            if (currentVersionError is not null)
            {
                failures.Add($"{table.TableName}: the current version could not write the upgraded table ({currentVersionError}).");
            }

            // The previous version writes only the columns that existed before the upgrade step ran. This is
            // the write a node that has not been restarted is still performing.
            var previousVersionError = await upgraded.TryInsertAsync(table.TableName, previousVersionColumns, documentId: 2);

            if (previousVersionError is not null)
            {
                var added = string.Join(", ", table.AddedColumns.Select(column => column.Describe()));

                failures.Add(
                    $"{table.TableName}: a node running the previous version can no longer write this table after the upgrade ({previousVersionError}). Columns added by the upgrade: {added}.");
            }

            var readable = await upgraded.CountReadableAsync(table.TableName, previousVersionColumns);

            if (readable < 2)
            {
                failures.Add($"{table.TableName}: the previous version's projection returned {readable} rows, expected 2.");
            }
        }

        Assert.True(exercised > 0, "No upgraded table was exercised.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public async Task UpgradePath_ProducesTheSameColumnDefinitions_AsAFreshInstall()
    {
        await using var fresh = await RollingUpgradeContext.CreateAsync();
        await fresh.ApplyFreshInstallAsync();
        await fresh.CaptureUpgradeStepsAsync();

        await using var upgraded = await RollingUpgradeContext.CreateAsync();
        await upgraded.ApplyPreviousVersionInstallAsync(fresh);
        await upgraded.ApplyUpgradeStepsAsync();

        var failures = new List<string>();
        var compared = 0;

        foreach (var table in fresh.Tables.Values.Where(candidate => candidate.AddedColumns.Count > 0))
        {
            var freshColumns = await fresh.ReadColumnDefinitionsAsync(table.TableName);
            var upgradedColumns = await upgraded.ReadColumnDefinitionsAsync(table.TableName);

            compared++;

            if (freshColumns.Count == 0)
            {
                failures.Add($"{table.TableName}: the fresh installation produced no table.");

                continue;
            }

            foreach (var (name, definition) in freshColumns)
            {
                if (!upgradedColumns.TryGetValue(name, out var upgradedDefinition))
                {
                    failures.Add($"{table.TableName}.{name}: present after a fresh installation but missing after the upgrade path.");

                    continue;
                }

                if (!string.Equals(definition, upgradedDefinition, StringComparison.Ordinal))
                {
                    failures.Add($"{table.TableName}.{name}: fresh installation declares '{definition}' but the upgrade path produced '{upgradedDefinition}'.");
                }
            }

            foreach (var name in upgradedColumns.Keys.Where(name => !freshColumns.ContainsKey(name)))
            {
                failures.Add($"{table.TableName}.{name}: produced by the upgrade path but absent from a fresh installation.");
            }

            var freshConstraints = await fresh.ReadUniqueConstraintsAsync(table.TableName);
            var upgradedConstraints = await upgraded.ReadUniqueConstraintsAsync(table.TableName);

            foreach (var columns in freshConstraints.Keys.Where(columns => !upgradedConstraints.ContainsKey(columns)))
            {
                failures.Add($"{table.TableName}({columns}): a fresh installation enforces uniqueness here but the upgrade path does not.");
            }

            foreach (var columns in upgradedConstraints.Keys.Where(columns => !freshConstraints.ContainsKey(columns)))
            {
                failures.Add($"{table.TableName}({columns}): the upgrade path enforces uniqueness here but a fresh installation does not.");
            }
        }

        Assert.True(compared > 0, "No upgraded table was compared.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private sealed class RollingUpgradeContext : IAsyncDisposable
    {
        private readonly string _databasePath;
        private readonly IStore _store;

        private RollingUpgradeContext(string databasePath, IStore store, List<DiscoveredMigration> migrations)
        {
            _databasePath = databasePath;
            _store = store;
            Migrations = migrations;
        }

        public List<DiscoveredMigration> Migrations { get; }

        public Dictionary<string, RecordedTable> Tables { get; } = new(StringComparer.OrdinalIgnoreCase);

        public static async Task<RollingUpgradeContext> CreateAsync()
        {
            var databasePath = Path.Combine(Path.GetTempPath(), $"cc-rolling-{Guid.NewGuid():N}.db");
            var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
            await store.InitializeAsync(TestContext.Current.CancellationToken);
            await store.InitializeCollectionAsync(CollectionName);

            return new RollingUpgradeContext(databasePath, store, DiscoverMigrations(store));
        }

        public async Task ApplyFreshInstallAsync()
        {
            await ExecuteAsync(async builder =>
            {
                foreach (var migration in Migrations)
                {
                    var owned = new Dictionary<string, RecordedTable>(StringComparer.OrdinalIgnoreCase);

                    migration.Instance.SchemaBuilder = new RecordingSchemaBuilder(_store.Configuration, builder, owned);
                    migration.CreateVersion = await migration.InvokeCreateAsync();

                    // Orchard does not stop at the version the create step reports. It keeps applying update
                    // steps until none matches, so a create step that returns an older version is completed by
                    // the chain. Modelling a fresh installation as the create step alone would report every
                    // column that chain adds as missing from a fresh installation.
                    foreach (var step in migration.AppliedSteps)
                    {
                        migration.Instance.SchemaBuilder = new RecordingSchemaBuilder(_store.Configuration, builder, owned);
                        await DiscoveredMigration.InvokeAsync(migration.Instance, step);
                    }

                    foreach (var entry in owned)
                    {
                        migration.TableNames.Add(entry.Key);
                        Tables[entry.Key] = entry.Value;
                    }
                }
            });
        }

        /// <summary>
        /// Captures what each upgrade step declares, on a transaction that is rolled back, so the previous
        /// version's schema can be derived by subtraction without the capture altering the database.
        /// </summary>
        public async Task CaptureUpgradeStepsAsync()
        {
            await using var connection = _store.Configuration.ConnectionFactory.CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(
                _store.Configuration.IsolationLevel,
                TestContext.Current.CancellationToken);

            var context = new SchemaBuilder(_store.Configuration, transaction, throwOnError: true);

            foreach (var migration in Migrations)
            {
                foreach (var step in migration.LegacySteps)
                {
                    var recorder = new RecordingSchemaBuilder(_store.Configuration, inner: null, Tables, context);
                    recorder.BeginUpdateStep();
                    migration.Instance.SchemaBuilder = recorder;

                    try
                    {
                        // Schema operations are captured rather than executed, so the step runs against a
                        // table that already has its columns. Data work therefore succeeds, and an operation
                        // that is genuinely already present — a unique index the create step made — throws
                        // after the declarations this pass needs have already been recorded.
                        var result = step.Invoke(migration.Instance, null);

                        if (result is Task task)
                        {
                            await task;
                        }
                    }
                    catch (Exception)
                    {
                        // Intentionally ignored: see above. Under-recording cannot pass unnoticed, because the
                        // convergence test rebuilds the database from the captured subtraction and fails when
                        // a column was missed.
                    }
                }
            }

            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }

        public async Task ApplyPreviousVersionInstallAsync(RollingUpgradeContext reference)
        {
            // This context has its own migration instances, so the version each create step reports has not
            // been resolved yet. Without it every step would be misclassified and the previous-version
            // database would be built from the wrong shape.
            foreach (var migration in Migrations)
            {
                var source = reference.Migrations
                    .FirstOrDefault(candidate => candidate.Instance.GetType() == migration.Instance.GetType());

                if (source is null)
                {
                    continue;
                }

                migration.CreateVersion = source.CreateVersion;
                migration.TableNames.AddRange(source.TableNames);
            }

            await ExecuteAsync(async builder =>
            {
                foreach (var migration in Migrations)
                {
                    var applied = migration.AppliedSteps;

                    if (applied.Count > 0)
                    {
                        // The previous version is reachable for real: run the shipped create step and every
                        // applied step except the newest one. Nothing about the older schema is synthesised.
                        migration.Instance.SchemaBuilder = builder;
                        await migration.InvokeCreateAsync();

                        foreach (var step in applied.Take(applied.Count - 1))
                        {
                            await DiscoveredMigration.InvokeAsync(migration.Instance, step);
                        }

                        continue;
                    }

                    if (migration.LegacySteps.Count == 0)
                    {
                        migration.Instance.SchemaBuilder = builder;
                        await migration.InvokeCreateAsync();

                        continue;
                    }

                    // The create step already reports the current version, so the older create step is no
                    // longer in the tree. The legacy update step is the only surviving description of what
                    // that version lacked, so the older table is the current one minus what the step adds.
                    foreach (var tableName in migration.TableNames)
                    {
                        if (!reference.Tables.TryGetValue(tableName, out var table))
                        {
                            continue;
                        }

                        var addedNames = table.AddedColumns
                            .Select(column => column.Name)
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);

                        var previousVersionColumns = table.CreatedColumns
                            .Where(column => !addedNames.Contains(column.Name));

                        await builder.CreateMapIndexTableAsync(
                            table.IndexType,
                            create =>
                            {
                                foreach (var column in previousVersionColumns)
                                {
                                    create.Column(column.Name, column.DbType, definition =>
                                    {
                                        if (column.Length.HasValue)
                                        {
                                            definition.WithLength(column.Length);
                                        }

                                        if (column.IsNotNull)
                                        {
                                            definition.NotNull();
                                        }

                                        if (column.DefaultValue is not null)
                                        {
                                            definition.WithDefault(column.DefaultValue);
                                        }
                                    });
                                }
                            },
                            collection: CollectionName);
                    }
                }
            });
        }

        public async Task ApplyUpgradeStepsAsync()
        {
            await ExecuteAsync(async builder =>
            {
                foreach (var migration in Migrations)
                {
                    migration.Instance.SchemaBuilder = builder;

                    foreach (var step in migration.PendingUpgradeSteps)
                    {
                        await DiscoveredMigration.InvokeAsync(migration.Instance, step);
                    }
                }
            });
        }

        public async Task<string> TryInsertAsync(string tableName, List<RecordedColumn> columns, int documentId)
        {
            var names = new List<string> { "DocumentId" };
            var values = new List<string> { documentId.ToString(CultureInfo.InvariantCulture) };

            foreach (var column in columns)
            {
                names.Add($"\"{column.Name}\"");
                values.Add(SampleLiteral(column));
            }

            var statement = $"INSERT INTO \"{Prefixed(tableName)}\" ({string.Join(", ", names)}) VALUES ({string.Join(", ", values)})";

            await using var connection = _store.Configuration.ConnectionFactory.CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = statement;
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

                return null;
            }
            catch (DbException exception)
            {
                return exception.Message;
            }
        }

        public async Task<int> CountReadableAsync(string tableName, List<RecordedColumn> columns)
        {
            var projection = columns.Count == 0
                ? "\"DocumentId\""
                : string.Join(", ", columns.Select(column => $"\"{column.Name}\""));

            await using var connection = _store.Configuration.ConnectionFactory.CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM (SELECT {projection} FROM \"{Prefixed(tableName)}\")";

            var result = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

            return Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }

        public async Task<Dictionary<string, string>> ReadColumnDefinitionsAsync(string tableName)
        {
            var definitions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            await using var connection = _store.Configuration.ConnectionFactory.CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{Prefixed(tableName)}\")";

            await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                var name = reader.GetString(1);
                var type = reader.GetString(2);
                var notNull = reader.GetInt32(3);
                var defaultValue = await reader.IsDBNullAsync(4, TestContext.Current.CancellationToken)
                    ? "none"
                    : reader.GetString(4);

                definitions[name] = $"{type}:notnull={notNull}:default={defaultValue}";
            }

            return definitions;
        }

        /// <summary>
        /// Reads the unique constraints a table carries, keyed by the columns they cover. Two installations
        /// can agree on every column and still disagree here, because an inline unique column declaration and
        /// an explicitly created unique index enforce the same rule through different objects.
        /// </summary>
        public async Task<Dictionary<string, string>> ReadUniqueConstraintsAsync(string tableName)
        {
            var constraints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            await using var connection = _store.Configuration.ConnectionFactory.CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);

            var indexNames = new List<string>();

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = $"PRAGMA index_list(\"{Prefixed(tableName)}\")";

                await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

                while (await reader.ReadAsync(TestContext.Current.CancellationToken))
                {
                    if (reader.GetInt32(2) == 1)
                    {
                        indexNames.Add(reader.GetString(1));
                    }
                }
            }

            foreach (var indexName in indexNames)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = $"PRAGMA index_info(\"{indexName}\")";

                var columns = new List<string>();

                await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

                while (await reader.ReadAsync(TestContext.Current.CancellationToken))
                {
                    if (!await reader.IsDBNullAsync(2, TestContext.Current.CancellationToken))
                    {
                        columns.Add(reader.GetString(2));
                    }
                }

                if (columns.Count > 0)
                {
                    constraints[string.Join(",", columns)] = indexName;
                }
            }

            return constraints;
        }

        public async ValueTask DisposeAsync()
        {
            _store.Dispose();

            await Task.Yield();

            try
            {
                File.Delete(_databasePath);
            }
            catch (IOException)
            {
                // The file is a temporary artifact; a locked handle on the build agent is not a test failure.
            }
        }

        private string Prefixed(string tableName)
            => string.Concat(_store.Configuration.TablePrefix, tableName);

        private async Task ExecuteAsync(Func<ISchemaBuilder, Task> action)
        {
            await using var connection = _store.Configuration.ConnectionFactory.CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(
                _store.Configuration.IsolationLevel,
                TestContext.Current.CancellationToken);

            var builder = new SchemaBuilder(_store.Configuration, transaction, throwOnError: true);
            await action(builder);

            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        private static string SampleLiteral(RecordedColumn column)
        {
            var type = Nullable.GetUnderlyingType(column.DbType) ?? column.DbType;

            if (type == typeof(bool))
            {
                return "1";
            }

            if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            {
                return "'2026-01-01 00:00:00'";
            }

            if (type == typeof(string) || type == typeof(Guid))
            {
                var sample = "s";

                return $"'{sample}'";
            }

            if (type.IsEnum)
            {
                return "0";
            }

            return "1";
        }

        private static List<DiscoveredMigration> DiscoverMigrations(IStore store)
        {
            var providerIdentityResolver = new Mock<IProviderIdentityResolver>();
            providerIdentityResolver
                .Setup(resolver => resolver.Canonicalize(It.IsAny<string>()))
                .Returns<string>(value => value);

            var recipeMigrator = new Mock<IRecipeMigrator>();

            var migrations = new List<DiscoveredMigration>();

            var candidates = typeof(ContactCenterMigrationSql).Assembly.GetTypes()
                .Where(type => !type.IsAbstract && typeof(DataMigration).IsAssignableFrom(type))
                .Where(type => type.Namespace is not null && type.Namespace.Contains("ContactCenter", StringComparison.Ordinal))
                .Distinct();

            foreach (var type in candidates)
            {
                var constructor = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .OrderBy(candidate => candidate.GetParameters().Length)
                    .LastOrDefault();

                if (constructor is null)
                {
                    continue;
                }

                var arguments = new List<object>();
                var supported = true;

                foreach (var parameter in constructor.GetParameters())
                {
                    if (parameter.ParameterType == typeof(IStore))
                    {
                        arguments.Add(store);
                    }
                    else if (parameter.ParameterType == typeof(IProviderIdentityResolver))
                    {
                        arguments.Add(providerIdentityResolver.Object);
                    }
                    else if (parameter.ParameterType == typeof(IRecipeMigrator))
                    {
                        arguments.Add(recipeMigrator.Object);
                    }
                    else
                    {
                        supported = false;

                        break;
                    }
                }

                if (!supported)
                {
                    continue;
                }

                var instance = (DataMigration)constructor.Invoke([.. arguments]);

                var create = type.GetMethod("CreateAsync", BindingFlags.Public | BindingFlags.Instance);
                var upgradeSteps = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(method => method.Name.StartsWith("UpdateFrom", StringComparison.Ordinal) &&
                        method.Name.EndsWith("Async", StringComparison.Ordinal))
                    .OrderBy(method => method.Name, StringComparer.Ordinal)
                    .ToList();

                migrations.Add(new DiscoveredMigration(instance, create, upgradeSteps));
            }

            return migrations;
        }
    }

    private sealed class DiscoveredMigration
    {
        public DiscoveredMigration(DataMigration instance, MethodInfo create, List<MethodInfo> upgradeSteps)
        {
            Instance = instance;
            Create = create;
            UpgradeSteps = upgradeSteps;
        }

        public DataMigration Instance { get; }

        public MethodInfo Create { get; }

        public List<MethodInfo> UpgradeSteps { get; }

        public List<string> TableNames { get; } = [];

        public int CreateVersion { get; set; }

        /// <summary>
        /// The update steps a fresh installation runs, because the create step reports a version older than
        /// the step's source version.
        /// </summary>
        public List<MethodInfo> AppliedSteps
            => UpgradeSteps.Where(step => FromVersion(step) >= CreateVersion)
                .OrderBy(FromVersion)
                .ToList();

        /// <summary>
        /// The update steps a fresh installation never runs, because the create step already reports a version
        /// at or beyond them. These describe the upgrade path taken by an existing deployment.
        /// </summary>
        public List<MethodInfo> LegacySteps
            => UpgradeSteps.Where(step => FromVersion(step) < CreateVersion)
                .OrderBy(FromVersion)
                .ToList();

        /// <summary>
        /// The steps that move a previous-version database onto the current schema.
        /// </summary>
        public List<MethodInfo> PendingUpgradeSteps
        {
            get
            {
                var applied = AppliedSteps;

                if (applied.Count > 0)
                {
                    return [applied[applied.Count - 1]];
                }

                return LegacySteps;
            }
        }

        public async Task<int> InvokeCreateAsync()
        {
            if (Create is null)
            {
                return 1;
            }

            if (Create.Invoke(Instance, null) is Task<int> task)
            {
                return await task;
            }

            return 1;
        }

        public static async Task InvokeAsync(DataMigration instance, MethodInfo step)
        {
            if (step.Invoke(instance, null) is Task task)
            {
                await task;
            }
        }

        public static int FromVersion(MethodInfo step)
        {
            var digits = step.Name
                .Replace("UpdateFrom", string.Empty, StringComparison.Ordinal)
                .Replace("Async", string.Empty, StringComparison.Ordinal);

            return int.TryParse(digits, CultureInfo.InvariantCulture, out var version)
                ? version
                : int.MaxValue;
        }
    }
}
