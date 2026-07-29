using System.Linq.Expressions;
using System.Reflection;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services.Retention;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Proves the retention plan actually covers the database. A table that grows without a policy is invisible
/// until an operator runs out of disk, so the absence of a policy has to fail a build rather than wait to be
/// noticed, and an exemption has to be a written decision rather than an oversight.
/// </summary>
public sealed class ContactCenterRetentionCoverageTests
{
    private const int MinimumPolicyCount = 12;

    /// <summary>
    /// Tables that are deliberately not aged out, and why. Each entry is a decision that a reviewer can
    /// disagree with, which is the point: silence is not an option.
    /// </summary>
    private static readonly Dictionary<string, string> _exemptions = new(StringComparer.Ordinal)
    {
        ["ActivityQueueIndex"] = "Configuration. One row per configured queue, bounded by tenant setup rather than traffic.",
        ["ActivityQueueGroupIndex"] = "Configuration. One row per configured queue group.",
        ["AgentProfileIndex"] = "Reference data. One row per agent, bounded by headcount rather than traffic.",
        ["AgentQueueMembershipIndex"] = "Reference data. One row per agent and queue pairing.",
        ["AgentStateReasonCodeIndex"] = "Configuration. One row per configured reason code.",
        ["BusinessHoursCalendarIndex"] = "Configuration. One row per configured calendar.",
        ["ContactCenterEntryPointIndex"] = "Configuration. One row per configured entry point.",
        ["ContactCenterSkillIndex"] = "Configuration. One row per configured skill.",
        ["DialerProfileIndex"] = "Configuration. One row per configured dialer profile.",
        ["ContactCenterProjectionCheckpointIndex"] = "Bookkeeping. One row per projection handler; deleting one would replay that projection from the beginning.",
    };

    /// <summary>
    /// The timestamp each entity is aged from. These are settlement times on purpose: ageing a record from when
    /// it arrived, when it was last retried, or when it was due punishes exactly the records that waited longest
    /// and deletes work that has not finished.
    /// </summary>
    private static readonly Dictionary<string, string> _settlementColumns = new(StringComparer.Ordinal)
    {
        ["InteractionEvent"] = "OccurredUtc",
        ["Interaction"] = "EndedUtc",
        ["CallSession"] = "EndedUtc",
        ["QueueItem"] = "DequeuedUtc",
        ["ActivityReservation"] = "ModifiedUtc",
        ["ContactCenterOutboxMessage"] = "CreatedUtc",
        ["ProviderWebhookInboxMessage"] = "ProcessedUtc",
        ["ProviderCommand"] = "CompletedUtc",
        ["AgentSession"] = "LastHeartbeatUtc",
        ["ContactCenterEventMetric"] = "Date",
        ["ContactCenterEventMetricDelta"] = "CreatedUtc",
        ["ContactCenterProcessedEvent"] = "ProcessedUtc",
        ["CallbackRequest"] = "ModifiedUtc",
        ["ContactCenterWorkState"] = "ModifiedUtc",
    };

    /// <summary>
    /// The statuses each policy is allowed to treat as settled, declared by name so that widening a policy to a
    /// live status fails the build rather than silently deleting work in flight. An empty set declares that the
    /// entity has no terminal status and is purged by age alone, which every entry below must justify.
    /// </summary>
    private static readonly Dictionary<string, string[]> _terminalStatuses = new(StringComparer.Ordinal)
    {
        // Communication history has no workflow status; it is settled once it has ended.
        ["InteractionEvent"] = [],
        ["Interaction"] = [],
        ["CallSession"] = [],
        ["ContactCenterEventMetric"] = [],
        ["ContactCenterEventMetricDelta"] = [],
        ["ContactCenterProcessedEvent"] = [],
        ["AgentSession"] = [],
        // No terminal status exists: closure is owned by the CRM activity. Safe only because a purged work
        // state is recreated and re-seeded from the activity projection on next access.
        ["ContactCenterWorkState"] = [],
        ["QueueItem"] = ["QueueItemStatus.Completed", "QueueItemStatus.Removed"],
        // Accepted is deliberately absent: an accepted reservation is the live claim an agent holds.
        ["ActivityReservation"] = ["ReservationStatus.Rejected", "ReservationStatus.Expired", "ReservationStatus.Canceled"],
        ["ContactCenterOutboxMessage"] = ["OutboxMessageStatus.Completed", "OutboxMessageStatus.DeadLettered"],
        ["ProviderWebhookInboxMessage"] = ["ProviderWebhookInboxStatus.Completed", "ProviderWebhookInboxStatus.DeadLettered"],
        ["ProviderCommand"] = ["ProviderCommandStatus.Confirmed", "ProviderCommandStatus.Compensated", "ProviderCommandStatus.Failed"],
        // Promotion hands the work to an activity, which is the durable record from then on, so a promoted
        // callback is settled. Nothing yet moves a callback to an outcome status.
        ["CallbackRequest"] = ["CallbackRequestStatus.Scheduled", "CallbackRequestStatus.Completed", "CallbackRequestStatus.Canceled", "CallbackRequestStatus.Failed"],
    };

    /// <summary>
    /// Entities whose records are settled the moment they are written, so no finished state and no absent
    /// settlement time can exist. An event log entry, a deduplication marker and a daily metric bucket are all
    /// records of something that already happened: the first two are never written again, and a metric bucket is
    /// only ever accumulated into on the day it names, which any window of a day or more is already past.
    /// </summary>
    private static readonly HashSet<string> _settledOnCreation = new(StringComparer.Ordinal)
    {
        "InteractionEvent",
        "ContactCenterEventMetric",
        "ContactCenterEventMetricDelta",
        "ContactCenterProcessedEvent",
    };

    /// <summary>
    /// The production statement on each settlement path that stamps the timestamp its policy ages from. A policy
    /// that ages from a column nothing writes reads as complete and purges nothing, because every predicate
    /// rejects nulls first. Each settlement path is declared with the method that owns it, because a file-wide
    /// search is satisfied by any one of a type's several settlement methods: deleting the stamp from the single
    /// path that ends in a status no other method produces would otherwise leave the build green while every
    /// record settled that way became immortal.
    /// </summary>
    private static readonly Dictionary<string, (string RelativePath, string Method, string Assignment)[]> _settlementWriters = new(StringComparer.Ordinal)
    {
        ["Interaction"] =
        [
            ("src/Modules/CrestApps.OrchardCore.ContactCenter/Handlers/ContactCenterSoftPhoneEventHandler.cs", "ApplyTerminalState", "interaction.EndedUtc = "),
            ("src/Modules/CrestApps.OrchardCore.ContactCenter/Services/VoiceContactCenterCallRouter.cs", "TerminalizeInboundAsync", "interaction.EndedUtc = "),
        ],
        ["InteractionEvent"] =
        [
            ("src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/DefaultContactCenterEventPublisher.cs", "PublishAsync", "interactionEvent.OccurredUtc = "),
        ],
        ["CallSession"] =
        [
            ("src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/CallTopologyProjector.cs", "EndMonitorSession", "live.EndedUtc = "),
        ],
        ["QueueItem"] =
        [
            ("src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/ActivityQueueService.cs", "DequeueAsync", "queueItem.DequeuedUtc = "),
            ("src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/ProviderVoiceOfferSynchronizationService.cs", "ReconcileEndedOfferAsync", "queueItem.DequeuedUtc = "),
        ],
        ["ActivityReservation"] =
        [
            ("src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/ActivityReservationService.cs", "ReleaseAsync", "reservation.ModifiedUtc = "),
            ("src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/ActivityReservationService.cs", "CompensateAsync", "reservation.ModifiedUtc = "),
            ("src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/ProviderVoiceOfferSynchronizationService.cs", "ReconcileEndedOfferAsync", "reservation.ModifiedUtc = "),
        ],
        ["CallbackRequest"] =
        [
            ("src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/CallbackService.cs", "PromoteDueAsync", "callback.ModifiedUtc = "),
        ],
        ["ContactCenterOutboxMessage"] =
        [
            ("src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/ContactCenterOutbox.cs", "GetOrCreateMessageAsync", "CreatedUtc = now,"),
        ],
        ["ProviderWebhookInboxMessage"] =
        [
            ("src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/ProviderWebhookInbox.cs", "SettleClaimAsync", "message.ProcessedUtc = "),
            ("src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/ProviderWebhookInbox.cs", "ScheduleRetryAsync", "message.ProcessedUtc = "),
        ],
        ["ProviderCommand"] =
        [
            ("src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/ProviderCommandStateService.cs", "ApplyConfirmed", "command.CompletedUtc = "),
            ("src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/ProviderCommandStateService.cs", "FailAsync", "command.CompletedUtc = "),
            ("src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/ProviderCommandStateService.cs", "CompleteCompensationAsync", "command.CompletedUtc = "),
        ],
        ["AgentSession"] =
        [
            ("src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/AgentSessionService.cs", "ConnectAsync", "session.LastHeartbeatUtc = "),
            ("src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/AgentSessionService.cs", "HeartbeatAsync", "session.LastHeartbeatUtc = "),
        ],
        ["ContactCenterEventMetric"] =
        [
            ("src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/ContactCenterMetricRollupService.cs", "AddAsync", "Date = ContactCenterMetricDateKey.Parse(dateKey),"),
        ],
        ["ContactCenterEventMetricDelta"] =
        [
            ("src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/ContactCenterMetricsService.cs", "RecordAsync", "CreatedUtc = _clock.UtcNow,"),
        ],
        ["ContactCenterProcessedEvent"] =
        [
            ("src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/ContactCenterEventDeduplicationService.cs", "TryBeginAsync", "ProcessedUtc = _clock.UtcNow,"),
        ],
        ["ContactCenterWorkState"] =
        [
            ("src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/ContactCenterWorkStateService.cs", "MutateAsync", "workState.ModifiedUtc = "),
        ],
    };

    /// <summary>
    /// Which governance floors each entity is held by. Communication history is held for legal hold, the durable
    /// event log is additionally held for as long as a projection can be rebuilt from it, and deduplication
    /// markers are held for as long as a provider can still redeliver the event they suppress.
    /// </summary>
    private static readonly Dictionary<string, (bool LegalHold, bool ReplayHorizon, double EnvelopeDays)> _governanceFloors = new(StringComparer.Ordinal)
    {
        ["InteractionEvent"] = (true, true, 0),
        ["Interaction"] = (true, false, 0),
        ["CallSession"] = (true, false, 0),
        ["CallbackRequest"] = (true, false, 0),
        ["ContactCenterProcessedEvent"] = (false, false, 3),
        ["QueueItem"] = (false, false, 0),
        ["ActivityReservation"] = (false, false, 0),
        ["ContactCenterOutboxMessage"] = (false, false, 0),
        // The inbox row survives its own payload purely as a duplicate-detection tombstone, so it is floored by the
        // seven-day tombstone horizon the inbox already enforces, never by the shorter delivery envelope.
        ["ProviderWebhookInboxMessage"] = (false, false, 7),
        ["ProviderCommand"] = (false, false, 0),
        ["AgentSession"] = (false, false, 0),
        ["ContactCenterEventMetric"] = (false, false, 0),
        ["ContactCenterEventMetricDelta"] = (false, false, 0),
        ["ContactCenterWorkState"] = (false, false, 0),
    };

    [Fact]
    public void EveryContactCenterIndex_EitherHasARetentionPolicy_OrAWrittenExemption()
    {
        // Arrange
        var indexes = DiscoverIndexes();
        var covered = DiscoverPolicies()
            .Select(policy => policy.IndexType.Name)
            .ToHashSet(StringComparer.Ordinal);

        // Act
        var uncovered = indexes
            .Where(index => !covered.Contains(index.Name) && !_exemptions.ContainsKey(index.Name))
            .Select(index => index.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        // Assert
        Assert.True(
            uncovered.Count == 0,
            $"These Contact Center tables have no retention policy and no written exemption, so they grow forever: {string.Join(", ", uncovered)}.");
    }

    [Fact]
    public void RetentionDiscovery_FindsEveryPolicy_SoTheCoverageCheckCannotPassVacuously()
    {
        // Arrange
        var policies = DiscoverPolicies();

        // Act
        var indexes = DiscoverIndexes();

        // Assert
        Assert.True(
            policies.Count >= MinimumPolicyCount,
            $"Expected at least {MinimumPolicyCount} retention policies but discovered {policies.Count}. Policy discovery has broken, which would make the coverage assertion vacuous.");

        Assert.True(
            indexes.Count > policies.Count,
            $"Expected to discover more Contact Center indexes than policies but found {indexes.Count} indexes and {policies.Count} policies. Index discovery has broken.");
    }

    [Fact]
    public void NoExemption_NamesATableThatNoLongerExistsOrIsAlreadyCovered()
    {
        // Arrange
        var indexNames = DiscoverIndexes()
            .Select(index => index.Name)
            .ToHashSet(StringComparer.Ordinal);

        var covered = DiscoverPolicies()
            .Select(policy => policy.IndexType.Name)
            .ToHashSet(StringComparer.Ordinal);

        // Act
        var stale = _exemptions.Keys.Where(name => !indexNames.Contains(name)).ToList();
        var contradictory = _exemptions.Keys.Where(covered.Contains).ToList();
        var unexplained = _exemptions.Where(entry => string.IsNullOrWhiteSpace(entry.Value)).Select(entry => entry.Key).ToList();

        // Assert
        Assert.True(stale.Count == 0, $"These exemptions name tables that no longer exist: {string.Join(", ", stale)}.");
        Assert.True(contradictory.Count == 0, $"These tables are exempted and also covered by a policy, so the exemption is misleading: {string.Join(", ", contradictory)}.");
        Assert.True(unexplained.Count == 0, $"These exemptions give no reason: {string.Join(", ", unexplained)}.");
    }

    [Fact]
    public void EveryPolicy_IsRegistered_SoACoveredTableIsActuallyPurgedAtRuntime()
    {
        // Arrange
        var startupPath = Path.Combine(RepositoryRoot(), "src", "Modules", "CrestApps.OrchardCore.ContactCenter", "Startup.cs");

        Assert.True(File.Exists(startupPath), $"Could not find the Contact Center startup file at '{startupPath}'.");

        var startup = File.ReadAllText(startupPath);

        // Act
        var unregistered = DiscoverPolicies()
            .Select(policy => policy.GetType().Name)
            .Where(name => !startup.Contains($"IContactCenterRetentionPolicy, {name}>", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        // Assert
        Assert.True(
            unregistered.Count == 0,
            $"These retention policies exist but are never registered, so the table they claim to cover is never purged: {string.Join(", ", unregistered)}.");
    }

    [Fact]
    public void NoPolicy_PurgesByAgeAlone_WhenItsTableCanHoldALiveRecord()
    {
        // Arrange
        var cutoff = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        var failures = new List<string>();

        // Act
        foreach (var policy in DiscoverPolicies())
        {
            var index = policy.IndexType;
            var referenced = ReferencedIndexMembers(policy.GetExpiredPredicate(cutoff), index);
            var settlementColumn = _settlementColumns[policy.EntityName];

            if (_terminalStatuses[policy.EntityName].Length > 0)
            {
                // A finished state exists, so the policy must actually test it. Nullability of a timestamp is
                // not a liveness guard: a live record carries a timestamp too.
                var guardsStatus = referenced.Any(property =>
                    (Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType).IsEnum);

                if (!guardsStatus)
                {
                    failures.Add(
                        $"{policy.EntityName}: the entity declares finished states but the policy never tests one, so an in-flight record would be deleted on age alone.");
                }

                continue;
            }

            if (_settledOnCreation.Contains(policy.EntityName))
            {
                continue;
            }

            // No finished state exists, so the settlement column has to be one a live record can lack. That is a
            // weaker guarantee than a status test, which is why an entity only reaches this branch by declaring an
            // empty terminal-status set with a written justification beside it.
            var settlesByAbsence = referenced.Any(property =>
                string.Equals(property.Name, settlementColumn, StringComparison.Ordinal)
                && Nullable.GetUnderlyingType(property.PropertyType) is not null);

            if (!settlesByAbsence)
            {
                failures.Add(
                    $"{policy.EntityName}: the entity has no finished state and '{settlementColumn}' cannot be absent, so nothing distinguishes a settled record from a live one.");
            }
        }

        // Assert
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void EverySettlementColumn_IsWrittenByProductionCode_SoTheRecordsCanActuallyExpire()
    {
        // Arrange
        // A policy that ages from a timestamp nothing ever writes reads as complete and purges nothing: every
        // predicate rejects nulls first, so the table grows forever while the cycle reports the entity enabled
        // and drained. The backfill only dates rows that existed at upgrade time, so the defect is invisible on
        // a database that was seeded before the column arrived.
        var failures = new List<string>();

        // Act
        foreach (var policy in DiscoverPolicies())
        {
            if (!_settlementWriters.TryGetValue(policy.EntityName, out var writers))
            {
                failures.Add(
                    $"{policy.EntityName}: no settlement writer is declared, so nothing proves anything ever stamps '{_settlementColumns[policy.EntityName]}'.");

                continue;
            }

            foreach (var (relativePath, method, assignment) in writers)
            {
                var file = Path.Combine(RepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));

                if (!File.Exists(file))
                {
                    failures.Add($"{policy.EntityName}: the declared settlement writer '{relativePath}' no longer exists.");

                    continue;
                }

                if (!TryReadMethodBody(File.ReadAllText(file), method, out var body))
                {
                    failures.Add(
                        $"{policy.EntityName}: '{relativePath}' no longer declares '{method}', so the settlement path this policy depends on cannot be located.");

                    continue;
                }

                if (!body.Contains(assignment, StringComparison.Ordinal))
                {
                    failures.Add(
                        $"{policy.EntityName}: '{relativePath}.{method}' no longer contains '{assignment}', so records settled on that path would keep a null settlement time and could never be purged.");
                }
            }
        }

        // Assert
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void EveryMigrationThatWritesRawSql_QuotesItsIdentifiersThroughTheDialect_SoTheStatementIsNotSilentlyInert()
    {
        // Arrange
        // A hardcoded double quote is an identifier delimiter on some engines and a string literal delimiter on
        // others. Where it is read as a literal, a filter such as "Status" IN (2, 3) compares the constant text
        // "Status" to two numbers, matches nothing, and the statement succeeds having touched zero rows. Nothing
        // fails, so the entire pre-upgrade backlog stays immortal. Every identifier must come from the dialect.
        var migrationsPath = Path.Combine(RepositoryRoot(), "src", "Modules", "CrestApps.OrchardCore.ContactCenter", "Migrations");
        var rawSqlFiles = new List<string>();
        var failures = new List<string>();

        // Act
        foreach (var file in Directory.EnumerateFiles(migrationsPath, "*.cs"))
        {
            var source = File.ReadAllText(file);

            if (!source.Contains("UPDATE ", StringComparison.Ordinal) &&
                !source.Contains("ALTER TABLE", StringComparison.Ordinal) &&
                !source.Contains("CREATE INDEX", StringComparison.Ordinal))
            {
                continue;
            }

            rawSqlFiles.Add(Path.GetFileName(file));

            if (source.Contains("\\\"", StringComparison.Ordinal))
            {
                failures.Add(
                    $"{Path.GetFileName(file)}: raw SQL quotes an identifier with a hardcoded double quote instead of going through SchemaBuilder.Dialect. On an engine that reads that quote as a string literal the statement becomes inert and skips every row without failing.");
            }

            if (!source.Contains("SchemaBuilder.Dialect", StringComparison.Ordinal) &&
                !source.Contains("_dialect", StringComparison.Ordinal) &&
                !source.Contains("ContactCenterMigrationSql", StringComparison.Ordinal))
            {
                failures.Add(
                    $"{Path.GetFileName(file)}: raw SQL is written without ever consulting the dialect, so its identifiers cannot be engine-correct on every supported database.");
            }
        }

        // Assert
        Assert.True(
            rawSqlFiles.Count >= 5,
            $"Only {rawSqlFiles.Count} migrations executing raw SQL were found, so this gate is not reading the files it is meant to check.");

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void EveryPolicy_ScansACoveringIndex_SoTheDrainDoesNotFullScanTheTableItIsDraining()
    {
        // Arrange
        // The purge selects a batch by settlement time with no ordering, so without an index leading with that
        // column every terminating batch is a full scan. At steady state that is a scan of the largest tables in
        // the schema on every cycle, which is the condition retention exists to prevent.
        var migrationsPath = Path.Combine(RepositoryRoot(), "src", "Modules", "CrestApps.OrchardCore.ContactCenter", "Migrations");

        var sources = Directory.EnumerateFiles(migrationsPath, "*IndexMigrations.cs")
            .Select(File.ReadAllText)
            .ToList();

        var failures = new List<string>();

        // Act
        foreach (var policy in DiscoverPolicies())
        {
            var indexName = policy.IndexType.Name;
            var column = _settlementColumns[policy.EntityName];
            var declaration = $"\"IDX_{indexName}_Retention\"";

            var covered = sources.Any(source =>
            {
                var start = source.IndexOf(declaration, StringComparison.Ordinal);

                if (start < 0)
                {
                    return false;
                }

                var end = source.IndexOf(");", start, StringComparison.Ordinal);

                return end > start && source.AsSpan(start, end - start).Contains($"\"{column}\"", StringComparison.Ordinal);
            });

            if (!covered)
            {
                failures.Add(
                    $"{policy.EntityName}: no migration creates 'IDX_{indexName}_Retention' over '{column}', so every purge batch scans the whole table.");
            }
        }

        // Assert
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void EveryUpgradeStep_ThatAddsASettlementColumn_BackfillsIt_SoTheLegacyBacklogIsNotImmortal()
    {
        // Arrange
        // Adding a column does not re-project documents that already exist, and a settled record is never
        // written again, so without a backfill every row that predates the upgrade keeps a null settlement time.
        // Every predicate starts by rejecting nulls, which would make the pre-upgrade backlog immortal while the
        // cycle reported success.
        var settlementColumns = _settlementColumns.Values.ToHashSet(StringComparer.Ordinal);
        var migrationsPath = Path.Combine(RepositoryRoot(), "src", "Modules", "CrestApps.OrchardCore.ContactCenter", "Migrations");
        var failures = new List<string>();

        // Act
        foreach (var file in Directory.EnumerateFiles(migrationsPath, "*IndexMigrations.cs"))
        {
            var source = File.ReadAllText(file);
            var steps = source.Split("public async Task<int> UpdateFrom", StringSplitOptions.None).Skip(1);

            foreach (var step in steps)
            {
                var stepName = step[..step.IndexOf('(', StringComparison.Ordinal)];

                foreach (var column in settlementColumns)
                {
                    if (!AddsColumn(step, column))
                    {
                        continue;
                    }

                    if (!step.Contains("AddRetentionColumnAsync", StringComparison.Ordinal)
                        || !step.Contains($"\"{column}\",", StringComparison.Ordinal))
                    {
                        failures.Add(
                            $"{Path.GetFileName(file)}: 'UpdateFrom{stepName}' adds the settlement column '{column}' but never backfills it, so every record that predates the upgrade can never be purged.");
                    }
                }
            }
        }

        // Assert
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void TheBackfillGate_FindsMigrationsThatAddSettlementColumns_SoItCannotPassVacuously()
    {
        // Arrange
        var settlementColumns = _settlementColumns.Values.ToHashSet(StringComparer.Ordinal);
        var migrationsPath = Path.Combine(RepositoryRoot(), "src", "Modules", "CrestApps.OrchardCore.ContactCenter", "Migrations");

        // Act
        var covered = Directory.EnumerateFiles(migrationsPath, "*IndexMigrations.cs")
            .Select(File.ReadAllText)
            .SelectMany(source => source.Split("public async Task<int> UpdateFrom", StringSplitOptions.None).Skip(1))
            .Count(step => settlementColumns.Any(column => AddsColumn(step, column)));

        // Assert
        Assert.True(
            covered >= 8,
            $"Only {covered} upgrade steps were found to add a settlement column. The backfill gate reads migration source, so a rename would silently turn it into a no-op.");
    }

    [Fact]
    public void EveryPolicy_TreatsOnlyDeclaredTerminalStatuses_AsExpired()
    {
        // Arrange
        var cutoff = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        var failures = new List<string>();

        // Act
        foreach (var policy in DiscoverPolicies())
        {
            if (!_terminalStatuses.TryGetValue(policy.EntityName, out var declared))
            {
                failures.Add(
                    $"{policy.EntityName}: no terminal status set is declared. Declare which statuses are settled, or declare an empty set and justify why the entity is purged by age alone.");

                continue;
            }

            var predicate = policy.GetExpiredPredicate(cutoff);
            var referenced = ReferencedEnumConstants(predicate)
                .OrderBy(name => name, StringComparer.Ordinal);

            var expected = declared.OrderBy(name => name, StringComparer.Ordinal);

            if (!referenced.SequenceEqual(expected, StringComparer.Ordinal))
            {
                failures.Add(
                    $"{policy.EntityName}: the policy treats [{string.Join(", ", referenced)}] as settled but [{string.Join(", ", expected)}] is declared. A status that is still live must never appear here. Predicate: {predicate}");
            }
        }

        // Assert
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void EveryPolicy_AgesARecordFromWhenItSettled_NotFromWhenItArrivedOrWasLastRetried()
    {
        // Arrange
        var cutoff = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        var failures = new List<string>();

        // Act
        foreach (var policy in DiscoverPolicies())
        {
            if (!_settlementColumns.TryGetValue(policy.EntityName, out var expected))
            {
                failures.Add($"{policy.EntityName}: no declared settlement column. Add one so the choice of age column is a reviewed decision rather than whichever timestamp was nearest.");

                continue;
            }

            var timestamps = ReferencedIndexMembers(policy.GetExpiredPredicate(cutoff), policy.IndexType)
                .Where(property => property.PropertyType == typeof(DateTime) || property.PropertyType == typeof(DateTime?))
                .Select(property => property.Name)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (timestamps.Count != 1 || !string.Equals(timestamps[0], expected, StringComparison.Ordinal))
            {
                failures.Add($"{policy.EntityName}: expected records to be aged by '{expected}' but the policy ages them by [{string.Join(", ", timestamps)}].");
            }
        }

        // Assert
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Theory]
    [InlineData(365, 30)]
    [InlineData(10, 730)]
    public void EveryPolicy_HoldsItsRecords_ForAsLongAsGovernanceRequires(int legalHoldDays, int replayHorizonDays)
    {
        // Arrange
        var nowUtc = new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc);

        // Every window is one day, so anything that survives longer is a floor doing its job. The two cases swap
        // which floor is the larger one, otherwise the dominant floor would hide the loss of the other.
        var options = new ContactCenterRetentionOptions
        {
            InteractionEventRetentionDays = 1,
            InteractionRetentionDays = 1,
            CallSessionRetentionDays = 1,
            QueueItemRetentionDays = 1,
            ActivityReservationRetentionDays = 1,
            OutboxMessageRetentionDays = 1,
            WebhookInboxMessageRetentionDays = 1,
            ProviderCommandRetentionDays = 1,
            AgentSessionRetentionDays = 1,
            CallbackRequestRetentionDays = 1,
            EventMetricRetentionDays = 1,
            ProcessedEventRetentionDays = 1,
            WorkStateRetentionDays = 1,
            ProcessedEventDeliveryEnvelopeDays = 3,
            ProjectionReplayHorizonDays = replayHorizonDays,
            LegalHoldMinimumDays = legalHoldDays,
        };

        var failures = new List<string>();

        // Act
        foreach (var policy in DiscoverPolicies())
        {
            if (!_governanceFloors.TryGetValue(policy.EntityName, out var floors))
            {
                failures.Add($"{policy.EntityName}: no declared governance floor. Declare whether this data is held by legal hold, by the replay horizon, by a redelivery envelope, or by nothing at all.");

                continue;
            }

            var expectedDays = Math.Max(
                1d,
                Math.Max(
                    floors.EnvelopeDays,
                    Math.Max(
                        floors.LegalHold ? legalHoldDays : 0d,
                        floors.ReplayHorizon ? replayHorizonDays : 0d)));

            Assert.True(policy.TryGetCutoff(nowUtc, options, out var cutoffUtc), $"{policy.EntityName}: purging is disabled even though a window is configured.");

            var actualDays = (nowUtc - cutoffUtc).TotalDays;

            if (Math.Abs(actualDays - expectedDays) > 0.0001)
            {
                failures.Add($"{policy.EntityName}: expected records to be held for {expectedDays} days but the policy purges anything older than {actualDays} days.");
            }
        }

        // Assert
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void EveryPolicy_ReportsPurgingDisabled_WhenItsWindowIsZero()
    {
        // Arrange
        var nowUtc = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);
        var options = new ContactCenterRetentionOptions();
        var failures = new List<string>();

        // Act
        foreach (var policy in DiscoverPolicies())
        {
            if (policy.TryGetCutoff(nowUtc, options, out _))
            {
                failures.Add($"{policy.EntityName}: purging is enabled even though no retention window is configured, so an unconfigured tenant would silently start deleting data.");
            }
        }

        // Assert
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static HashSet<string> ReferencedEnumConstants(LambdaExpression predicate)
    {
        var collector = new EnumConstantCollector();
        collector.Visit(predicate);

        return collector.Constants;
    }

    private static List<PropertyInfo> ReferencedIndexMembers(LambdaExpression predicate, Type indexType)
    {
        var collector = new IndexMemberCollector(indexType);

        collector.Visit(predicate);

        return collector.Members;
    }

    private static List<Type> DiscoverIndexes()
        => typeof(CallSessionIndex).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(IIndex).IsAssignableFrom(type))
            .Where(type => type.Namespace is not null && type.Namespace.EndsWith("ContactCenter.Core.Indexes", StringComparison.Ordinal))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToList();

    private static List<IContactCenterRetentionPolicy> DiscoverPolicies()
    {
        var policies = new List<IContactCenterRetentionPolicy>();

        var candidates = typeof(IContactCenterRetentionPolicy).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(IContactCenterRetentionPolicy).IsAssignableFrom(type))
            .OrderBy(type => type.Name, StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            var constructor = candidate.GetConstructors().Single();
            var arguments = constructor.GetParameters()
                .Select(parameter => (object)null)
                .ToArray();

            policies.Add((IContactCenterRetentionPolicy)constructor.Invoke(arguments));
        }

        return policies;
    }

    private static bool AddsColumn(string step, string column)
        => step.Contains($".AddColumn<DateTime>(\"{column}\"", StringComparison.Ordinal)
            || step.Contains($".AddColumn<DateTime?>(\"{column}\"", StringComparison.Ordinal);

    /// <summary>
    /// Extracts the body of the named method by brace matching, so a settlement stamp can be required on the
    /// specific path that produces a terminal status instead of anywhere in the declaring file.
    /// </summary>
    /// <param name="source">The full C# source of the file that declares the method.</param>
    /// <param name="method">The method name to locate.</param>
    /// <param name="body">The matched method body, or an empty string when the method was not found.</param>
    private static bool TryReadMethodBody(string source, string method, out string body)
    {
        body = string.Empty;
        var searchFrom = 0;

        while (true)
        {
            var nameAt = source.IndexOf(method, searchFrom, StringComparison.Ordinal);

            if (nameAt < 0)
            {
                return false;
            }

            searchFrom = nameAt + method.Length;

            // A declaration is an identifier immediately followed by its parameter list. A call site is preceded
            // by a dot or an await, so requiring the preceding character to be whitespace excludes both.
            if (nameAt == 0 || !char.IsWhiteSpace(source[nameAt - 1]) || searchFrom >= source.Length || source[searchFrom] != '(')
            {
                continue;
            }

            var openParen = searchFrom;
            var closeParen = source.IndexOf(')', openParen);

            if (closeParen < 0)
            {
                return false;
            }

            var openBrace = source.IndexOf('{', closeParen);
            var terminator = source.IndexOf(';', closeParen);

            // An expression-bodied or abstract declaration ends before it ever opens a block.
            if (openBrace < 0 || (terminator >= 0 && terminator < openBrace))
            {
                continue;
            }

            var depth = 0;

            for (var i = openBrace; i < source.Length; i++)
            {
                if (source[i] == '{')
                {
                    depth++;
                }
                else if (source[i] == '}')
                {
                    depth--;

                    if (depth == 0)
                    {
                        body = source.Substring(openBrace + 1, i - openBrace - 1);

                        return true;
                    }
                }
            }

            return false;
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return directory.FullName;
    }

    private sealed class IndexMemberCollector : ExpressionVisitor
    {
        private readonly Type _indexType;

        public IndexMemberCollector(Type indexType)
        {
            _indexType = indexType;
        }

        public List<PropertyInfo> Members { get; } = [];

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression is ParameterExpression parameter
                && parameter.Type == _indexType
                && node.Member is PropertyInfo property)
            {
                Members.Add(property);
            }

            return base.VisitMember(node);
        }
    }

    /// <summary>
    /// Collects the enum constants a predicate compares against. The C# compiler erases the enum type in an
    /// expression tree, emitting 'Convert(index.Status, Int32) == 3', so the type is recovered from the member
    /// side of the comparison and the integer is converted back into the name a reviewer can read.
    /// </summary>
    private sealed class EnumConstantCollector : ExpressionVisitor
    {
        public HashSet<string> Constants { get; } = new(StringComparer.Ordinal);

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual)
            {
                TryCollect(node.Left, node.Right);
                TryCollect(node.Right, node.Left);
            }

            return base.VisitBinary(node);
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value is Enum value)
            {
                Constants.Add($"{value.GetType().Name}.{value}");
            }

            return base.VisitConstant(node);
        }

        private void TryCollect(Expression memberSide, Expression constantSide)
        {
            var enumType = UnwrapEnumType(memberSide);

            if (enumType is null || constantSide is not ConstantExpression constant || constant.Value is null)
            {
                return;
            }

            if (constant.Value is Enum)
            {
                return;
            }

            Constants.Add($"{enumType.Name}.{Enum.ToObject(enumType, constant.Value)}");
        }

        private static Type UnwrapEnumType(Expression expression)
        {
            while (expression is UnaryExpression unary && expression.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked)
            {
                expression = unary.Operand;
            }

            var type = Nullable.GetUnderlyingType(expression.Type) ?? expression.Type;

            return type.IsEnum ? type : null;
        }
    }
}
