using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

/// <summary>
/// Proves that no supported Contact Center deployment depends on a search engine.
/// </summary>
/// <remarks>
/// The published support matrix declares Elasticsearch unsupported in routing, assignment, provider ingest, or any
/// other correctness path, and the supported single-node topology deploys no search cluster. Nothing enforced that,
/// so a routing or ingest path could acquire a search dependency and the claim would silently become false, leaving
/// supported deployments unable to run what the matrix promises.
/// <para>
/// A search dependency can be introduced through three independent mechanisms, and an oracle that covers one is
/// blind to the others. A CLR reference is the compile-time mechanism. An Orchard feature dependency is a *string*
/// in a manifest, which creates a runtime dependency with no CLR reference at all, so an assembly-only gate would
/// stay green while a Contact Center feature pulled an entire Elasticsearch module into every tenant that enabled
/// it. A plain HTTP call to a cluster has neither a reference nor a feature, so only execution reveals it. All
/// three mechanisms are therefore checked.
/// </para>
/// <para>
/// The assembly closure is read with <see cref="MetadataReader"/> rather than <see cref="Assembly.Load(AssemblyName)"/>.
/// Loading forces every transitive dependency to resolve, so one unrelated missing assembly aborts that branch of
/// the walk; swallowing the failure would let a violation hide behind an intermediate assembly that happened not to
/// load. Reading metadata off disk has no such failure mode, and traversal completeness is asserted rather than
/// assumed: every referenced assembly must either be resolved from the deployment or be a shared-framework
/// assembly, which cannot reference a search client.
/// </para>
/// <para>
/// The direct-reference ban is wider than the transitive one, and deliberately so. Measurement showed the closure
/// legitimately reaches an embedded Lucene index through shared libraries that Contact Center depends on for
/// unrelated reasons. An in-process index needs no cluster and no operator action, so it does not make a deployment
/// require Elasticsearch, which is what the matrix actually prohibits. Banning it transitively would report a
/// dependency the module does not have and would make the gate something engineers suppress rather than trust.
/// </para>
/// <para>
/// The static facts are paired with a runtime one that actually executes the correctness paths the support matrix
/// names — routing selection, assignment through a real reservation, outbox dispatch, and provider ingest through
/// the normalized voice-event seam every PBX adapter funnels into, including its replay-suppression behaviour —
/// inside a real supported-profile tenant, against persisted state, while every outbound HTTP request in the
/// process is recorded, and requires that none was issued. Absence of any egress is asserted rather than absence
/// of a search host specifically, because host matching only catches a regression that names its cluster
/// recognizably, whereas a supported single-node correctness path has no legitimate out-of-process dependency at
/// all.
/// </para>
/// <para>
/// What remains unproved is behaviour with the Elasticsearch binaries physically removed. Those assemblies are
/// present in the test output because the module bundle ships them for other, opt-in features. Excluding them needs
/// a separate packaging harness and is tracked as a follow-up rather than claimed here.
/// </para>
/// </remarks>
public sealed class ContactCenterSearchEngineIndependenceTests
{
    /// <summary>
    /// The supported tenant profiles whose enabled feature closure must stay free of search.
    /// </summary>
    private static readonly string[] _supportedProfileIds =
    [
        "ga-core-asterisk",
        "ga-core-dialpad",
    ];

    /// <summary>
    /// The assembly-name prefixes identifying an assembly this product ships for Contact Center.
    /// </summary>
    private static readonly string[] _shippedAssemblyPrefixes =
    [
        "CrestApps.OrchardCore.ContactCenter",
        "CrestApps.OrchardCore.Telephony",
        "CrestApps.OrchardCore.Asterisk",
        "CrestApps.OrchardCore.DialPad",
    ];

    /// <summary>
    /// The shipped assemblies the scan must find, pinning discovery so the gate cannot pass by scanning nothing.
    /// </summary>
    private static readonly string[] _expectedShippedAssemblies =
    [
        "CrestApps.OrchardCore.Asterisk",
        "CrestApps.OrchardCore.ContactCenter",
        "CrestApps.OrchardCore.ContactCenter.Abstractions",
        "CrestApps.OrchardCore.ContactCenter.Core",
        "CrestApps.OrchardCore.DialPad",
        "CrestApps.OrchardCore.Telephony",
        "CrestApps.OrchardCore.Telephony.Abstractions",
        "CrestApps.OrchardCore.Telephony.Core",
    ];

    /// <summary>
    /// Name fragments identifying a client for an externally deployed search cluster.
    /// </summary>
    private static readonly string[] _searchClusterFragments =
    [
        "Elasticsearch",
        "OpenSearch",
        "Elastic.Clients",
        "Elastic.Transport",
    ];

    /// <summary>
    /// Name fragments identifying a search engine, whether it runs as a cluster or in process.
    /// </summary>
    /// <remarks>
    /// This is the set that matters for an enabled feature. It excludes the Orchard search and indexing
    /// infrastructure features, which provide index management abstractions and have no engine behind them: a
    /// deployment can enable them and still need no search product installed. Measurement confirmed the platform
    /// baseline enables <c>OrchardCore.Indexing</c> in every tenant regardless of what Contact Center asks for, so
    /// treating it as a search dependency would report the platform rather than this product.
    /// </remarks>
    private static readonly string[] _searchEngineFragments =
    [
        .. _searchClusterFragments,
        "Lucene",
    ];

    /// <summary>
    /// Name fragments identifying any search or indexing surface, cluster-backed or embedded.
    /// </summary>
    private static readonly string[] _searchSurfaceFragments =
    [
        .. _searchEngineFragments,
        "OrchardCore.Search",
        "OrchardCore.Indexing",
    ];

    [Fact]
    public void NoShippedAssembly_TransitivelyReferencesASearchClusterClient()
    {
        var deployment = AssemblyDeployment.Scan();

        var violations = new List<string>();

        foreach (var assembly in deployment.ShippedAssemblies)
        {
            violations.AddRange(deployment.FindReferencePathsTo(assembly, _searchClusterFragments));
        }

        Assert.True(
            violations.Count == 0,
            Describe(
                "No Contact Center or Telephony assembly may reach a search cluster client, at any depth, because " +
                "the supported single-node topology deploys no search cluster.",
                "Move the cluster-backed capability into a separate opt-in module that a supported topology leaves " +
                "disabled, and keep the correctness path working with no search cluster deployed.",
                violations));
    }

    [Fact]
    public void NoShippedAssembly_DirectlyReferencesASearchOrIndexingApi()
    {
        var deployment = AssemblyDeployment.Scan();

        var violations = new List<string>();

        foreach (var assembly in deployment.ShippedAssemblies)
        {
            foreach (var reference in deployment.GetReferences(assembly))
            {
                if (Matches(reference, _searchSurfaceFragments))
                {
                    violations.Add($"{assembly} -> {reference}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            Describe(
                "No Contact Center or Telephony assembly may be written against a search or indexing API, because " +
                "the support matrix places search outside every correctness path.",
                "Keep search-backed behaviour in a separate opt-in module and let the correctness path read its " +
                "own durable state instead.",
                violations));
    }

    [Fact]
    public void TheAssemblyScan_IsCompleteAndCoversExactlyTheShippedAssemblies()
    {
        var deployment = AssemblyDeployment.Scan();

        Assert.Equal(_expectedShippedAssemblies, deployment.ShippedAssemblies);

        // A reference the scan could not resolve is a hole in the traversal, because its own references were never
        // examined. Shared-framework assemblies are the only legitimate case: they are not copied beside the
        // application and cannot reference a search client.
        var unresolved = deployment.GetUnresolvedNonFrameworkReferences();

        Assert.True(
            unresolved.Count == 0,
            Describe(
                "The assembly closure walk could not resolve every referenced assembly, so it cannot prove the " +
                "absence of a transitive search dependency.",
                "Ensure the referenced assembly is copied to the test output, or classify it as a shared-framework " +
                "assembly if it genuinely ships with the runtime.",
                unresolved));
    }

    [Fact]
    public async Task NoContactCenterFeature_DeclaresADependencyOnASearchBackedFeature()
    {
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var tenant = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = "search-independence-manifest",
            ProviderProfile = "none",
            Features = [ContactCenterConstants.Feature.Area],
        });

        var violations = await host.ExecuteInTenantScopeAsync(tenant, async serviceProvider =>
        {
            var featureManager = serviceProvider.GetRequiredService<IShellFeaturesManager>();
            var availableFeatures = (await featureManager.GetAvailableFeaturesAsync()).ToArray();
            var shipped = availableFeatures
                .Where(feature => IsShippedFeature(feature.Extension?.Id))
                .ToArray();

            var found = new List<string>();

            var byId = availableFeatures
                .GroupBy(feature => feature.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            // Walking the whole declared closure rather than only direct dependencies is what makes this bite for a
            // feature no supported profile enables. The profile fact would catch an indirect search dependency only
            // once something enabled it, so an unsupported feature could acquire one and stay invisible until a
            // customer turned it on.
            foreach (var feature in shipped)
            {
                var visited = new HashSet<string>(StringComparer.Ordinal) { feature.Id };
                var pending = new Queue<(string Id, string Path)>();

                foreach (var dependency in feature.Dependencies)
                {
                    pending.Enqueue((dependency, $"{feature.Id} -> {dependency}"));
                }

                while (pending.Count > 0)
                {
                    var (dependencyId, path) = pending.Dequeue();

                    if (!visited.Add(dependencyId))
                    {
                        continue;
                    }

                    if (Matches(dependencyId, _searchSurfaceFragments))
                    {
                        found.Add(path);

                        continue;
                    }

                    if (!byId.TryGetValue(dependencyId, out var dependency))
                    {
                        continue;
                    }

                    foreach (var transitive in dependency.Dependencies)
                    {
                        pending.Enqueue((transitive, $"{path} -> {transitive}"));
                    }
                }
            }

            // A manifest that declares no features at all would make this vacuous, which is the same failure mode
            // as an empty assembly scan.
            Assert.NotEmpty(shipped);

            return found;
        });

        Assert.True(
            violations.Count == 0,
            Describe(
                "No Contact Center or Telephony feature may declare a dependency on a search-backed feature. An " +
                "Orchard dependency is a string, so it creates a runtime dependency with no assembly reference and " +
                "would not be visible to the assembly gates. The whole declared closure is walked, because an " +
                "intermediate feature can carry the dependency just as effectively as a direct one.",
                "Remove the dependency and put the search-backed behaviour in a separate opt-in module.",
                violations));
    }

    [Theory]
    [InlineData("ga-core-asterisk")]
    [InlineData("ga-core-dialpad")]
    public async Task SupportedProfile_EnablesNoSearchBackedFeature(string profileId)
    {
        Assert.Contains(profileId, _supportedProfileIds);

        var matrix = await ContactCenterSupportMatrix.LoadAsync();
        var profile = matrix.TenantProfiles.Single(profile => profile.Id == profileId);

        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var tenant = await host.CreateTenantAsync(profile);

        var violations = await host.ExecuteInTenantScopeAsync(tenant, async serviceProvider =>
        {
            var featureManager = serviceProvider.GetRequiredService<IShellFeaturesManager>();
            var enabledFeatures = (await featureManager.GetEnabledFeaturesAsync()).ToArray();

            var found = new List<string>();

            foreach (var feature in enabledFeatures)
            {
                var extensionId = feature.Extension?.Id;

                if (!Matches(feature.Id, _searchEngineFragments) &&
                    (extensionId is null || !Matches(extensionId, _searchEngineFragments)))
                {
                    continue;
                }

                // Naming who pulled the feature in is what makes the failure actionable: the profile lists a few
                // features and Orchard enables their whole declared closure, so the cause is usually a manifest
                // several hops away rather than the profile itself.
                var requiredBy = enabledFeatures
                    .Where(candidate => candidate.Dependencies.Contains(feature.Id, StringComparer.Ordinal))
                    .Select(candidate => candidate.Id)
                    .Order(StringComparer.Ordinal)
                    .ToArray();

                var cause = requiredBy.Length == 0
                    ? "listed directly by the profile or enabled by the platform baseline"
                    : $"required by {string.Join(", ", requiredBy)}";

                found.Add($"Feature '{feature.Id}' from '{extensionId ?? "unknown"}' is enabled, {cause}.");
            }

            Assert.NotEmpty(enabledFeatures);

            return found;
        });

        Assert.True(
            violations.Count == 0,
            Describe(
                $"The supported profile '{profileId}' enables a search-backed feature, so the deployment it " +
                "describes would require a search engine the supported topology does not provide.",
                "Remove the feature from the profile, or remove the manifest dependency that pulls it into the " +
                "enabled closure.",
                violations));
    }

    [Theory]
    [InlineData("ga-core-asterisk")]
    [InlineData("ga-core-dialpad")]
    public async Task SupportedProfile_RunsItsCorrectnessPathsWithoutCallingOutOfProcess(string profileId)
    {
        Assert.Contains(profileId, _supportedProfileIds);

        var matrix = await ContactCenterSupportMatrix.LoadAsync();
        var profile = matrix.TenantProfiles.Single(candidate => candidate.Id == profileId);

        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var tenant = await host.CreateTenantAsync(profile);

        using var egress = new HttpEgressRecorder();

        await egress.EnsureObservingAsync();

        await host.ExecuteInTenantScopeAsync(tenant, async serviceProvider =>
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var session = serviceProvider.GetRequiredService<ISession>();
            var clock = serviceProvider.GetRequiredService<IClock>();
            var now = clock.UtcNow;

            var queueManager = serviceProvider.GetRequiredService<IActivityQueueManager>();
            var queue = await queueManager.NewAsync(cancellationToken: cancellationToken);
            queue.Name = "Search independence";
            queue.Enabled = true;
            queue.RoutingStrategy = QueueRoutingStrategy.LongestIdle;
            queue.ReservationTimeoutSeconds = 30;
            await queueManager.CreateAsync(queue, cancellationToken: cancellationToken);

            var queueItemManager = serviceProvider.GetRequiredService<IQueueItemManager>();
            var queueItem = await queueItemManager.NewAsync(cancellationToken: cancellationToken);
            queueItem.QueueId = queue.ItemId;
            queueItem.ActivityItemId = "activity-search-independence";
            queueItem.Status = QueueItemStatus.Waiting;
            queueItem.Priority = InteractionPriority.Normal;
            queueItem.EnqueuedUtc = now;
            queueItem.QueueEnteredUtc = now;
            await queueItemManager.CreateAsync(queueItem, cancellationToken: cancellationToken);

            var agentManager = serviceProvider.GetRequiredService<IAgentProfileManager>();
            var agent = await agentManager.NewAsync(cancellationToken: cancellationToken);
            agent.Name = "Search independence agent";
            agent.UserId = "user-search-independence";
            agent.UserName = "search-independence";
            agent.PresenceStatus = AgentPresenceStatus.Available;
            agent.MaxConcurrentInteractions = 1;
            agent.QueueIds = [queue.ItemId];

            // Queue entitlement fails closed, so signing in to a queue is not enough on its own.
            agent.AllowedQueueIds = [queue.ItemId];
            agent.CreatedUtc = now;
            await agentManager.CreateAsync(agent, cancellationToken: cancellationToken);

            var sessionManager = serviceProvider.GetRequiredService<IAgentSessionManager>();
            var agentSession = await sessionManager.NewAsync(cancellationToken: cancellationToken);
            agentSession.UserId = agent.UserId;
            agentSession.UserName = agent.UserName;
            agentSession.IsOnline = true;
            agentSession.ConnectionIds = ["connection-search-independence"];
            agentSession.QueueIds = [queue.ItemId];
            agentSession.ConnectedUtc = now;
            agentSession.LastHeartbeatUtc = now;
            agentSession.CreatedUtc = now;
            await sessionManager.CreateAsync(agentSession, cancellationToken: cancellationToken);

            await session.SaveChangesAsync(cancellationToken);

            // Routing selection.
            var routing = serviceProvider.GetRequiredService<IActivityRoutingService>();
            var decision = await routing.SelectAgentAsync(queue, queueItem, [agent], cancellationToken);
            Assert.True(decision.Succeeded, decision.Reason);
            Assert.Equal(agent.ItemId, decision.Agent.ItemId);

            // Assignment, which must reach a real reservation rather than falling out on missing state.
            var assignment = serviceProvider.GetRequiredService<IActivityAssignmentService>();
            var reservation = await assignment.AssignNextAsync(queue.ItemId, cancellationToken);
            Assert.NotNull(reservation);
            Assert.Equal(queueItem.ItemId, reservation.QueueItemId);

            // Event dispatch, through enqueue, immediate dispatch, and the due-message drain.
            var outbox = serviceProvider.GetRequiredService<IContactCenterOutbox>();

            var interactionEvent = new InteractionEvent
            {
                ItemId = "event-search-independence",
                InteractionId = "interaction-search-independence",
                EventType = ContactCenterConstants.Events.AgentReserved,
                AggregateType = nameof(QueueItem),
                AggregateId = queueItem.ItemId,
                IdempotencyKey = "search-independence-probe",
                SourceComponent = nameof(ContactCenterSearchEngineIndependenceTests),
                OccurredUtc = now,
            };

            await outbox.EnqueueAsync(interactionEvent, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
            await outbox.DispatchAsync(interactionEvent, cancellationToken);
            await outbox.DispatchDueAsync(cancellationToken);

            // Provider ingest, through the normalized voice-event seam every PBX adapter funnels into. This
            // is the path that acquires the distributed ingestion lock, resolves the interaction through its
            // provider index, de-duplicates through the interaction event store, and publishes downstream.
            var interactionManager = serviceProvider.GetRequiredService<IInteractionManager>();
            var interaction = await interactionManager.NewAsync(cancellationToken: cancellationToken);
            interaction.Channel = InteractionChannel.Voice;
            interaction.Direction = InteractionDirection.Inbound;
            interaction.Status = InteractionStatus.Ringing;
            interaction.QueueId = queue.ItemId;
            interaction.ActivityItemId = queueItem.ActivityItemId;
            interaction.ProviderName = profile.ProviderProfile;
            interaction.ProviderInteractionId = "call-search-independence";
            interaction.StartedUtc = now;
            await interactionManager.CreateAsync(interaction, cancellationToken: cancellationToken);

            await session.SaveChangesAsync(cancellationToken);

            var voiceEvents = serviceProvider.GetRequiredService<IProviderVoiceEventService>();

            var callSession = await voiceEvents.IngestAsync(
                new ProviderVoiceEvent
                {
                    ProviderName = interaction.ProviderName,
                    ProviderCallId = interaction.ProviderInteractionId,
                    State = ContactCenterCallState.Connected,
                    OccurredUtc = now,
                    IdempotencyKey = "search-independence-ingest",
                },
                cancellationToken);

            Assert.NotNull(callSession);
            Assert.Equal(ContactCenterCallState.Connected, callSession.State);

            // Replaying the same delivery must be absorbed rather than double-applied, which is the property
            // that makes provider ingest safe to retry without a search engine in the path.
            var replayed = await voiceEvents.IngestAsync(
                new ProviderVoiceEvent
                {
                    ProviderName = interaction.ProviderName,
                    ProviderCallId = interaction.ProviderInteractionId,
                    State = ContactCenterCallState.Ended,
                    OccurredUtc = now,
                    IdempotencyKey = "search-independence-ingest",
                },
                cancellationToken);

            Assert.Equal(ContactCenterCallState.Connected, replayed.State);
        });

        var observed = egress.GetObservedRequests();

        Assert.True(
            observed.Count == 0,
            Describe(
                $"Routing, assignment, event dispatch, and provider ingest under the supported profile " +
                $"'{profileId}' issued outbound HTTP requests. A supported single-node correctness path must " +
                "complete without calling any out-of-process service, so this either reaches a search cluster " +
                "or externalizes a correctness path some other way.",
                "Keep the correctness path self-contained, or move the out-of-process call into an opt-in " +
                "feature a supported topology leaves disabled.",
                observed));
    }

    /// <summary>
    /// Determines whether a name identifies an assembly or extension this product ships for Contact Center.
    /// </summary>
    /// <param name="assemblyName">The simple assembly name to classify, which may be <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the assembly is a shipped Contact Center assembly.</returns>
    private static bool IsShippedFeature(string assemblyName)
        => assemblyName is not null &&
            _shippedAssemblyPrefixes.Any(prefix => assemblyName.StartsWith(prefix, StringComparison.Ordinal));

    /// <summary>
    /// Determines whether a name contains any of the supplied fragments.
    /// </summary>
    /// <param name="value">The name to classify.</param>
    /// <param name="fragments">The fragments to match against.</param>
    /// <returns><see langword="true"/> when the name matches a fragment.</returns>
    private static bool Matches(string value, string[] fragments)
        => fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Builds an assertion message that names every violation and explains how to resolve it.
    /// </summary>
    /// <param name="summary">What the gate found.</param>
    /// <param name="remedy">How to resolve the violations.</param>
    /// <param name="violations">The individual violations.</param>
    /// <returns>The assertion message.</returns>
    private static string Describe(string summary, string remedy, IEnumerable<string> violations)
    {
        var message = new StringBuilder(summary).AppendLine().AppendLine();

        foreach (var violation in violations.Order(StringComparer.Ordinal))
        {
            message.Append("  - ").AppendLine(violation);
        }

        return message.AppendLine().Append(remedy).ToString();
    }

    /// <summary>
    /// The set of managed assemblies deployed beside the application, and their reference graph.
    /// </summary>
    /// <remarks>
    /// References are read from assembly metadata rather than by loading, so an assembly whose own dependencies are
    /// missing still contributes its references to the graph. That is what makes the traversal complete enough to
    /// support a negative claim.
    /// </remarks>
    private sealed class AssemblyDeployment
    {
        private readonly Dictionary<string, ImmutableArray<string>> _referencesByAssembly;

        private AssemblyDeployment(Dictionary<string, ImmutableArray<string>> referencesByAssembly)
        {
            _referencesByAssembly = referencesByAssembly;

            ShippedAssemblies = referencesByAssembly.Keys
                .Where(name => IsShippedFeature(name) && !IsTestAssembly(name))
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Gets the shipped Contact Center assemblies found in the deployment, in name order.
        /// </summary>
        public string[] ShippedAssemblies { get; }

        /// <summary>
        /// Reads every managed assembly beside the running tests and records its assembly references.
        /// </summary>
        /// <returns>The deployment graph.</returns>
        public static AssemblyDeployment Scan()
        {
            var referencesByAssembly = new Dictionary<string, ImmutableArray<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var file in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll"))
            {
                var name = Path.GetFileNameWithoutExtension(file);

                if (referencesByAssembly.ContainsKey(name))
                {
                    continue;
                }

                if (TryReadReferences(file, out var references))
                {
                    referencesByAssembly[name] = references;
                }
            }

            return new AssemblyDeployment(referencesByAssembly);
        }

        /// <summary>
        /// Gets the assemblies referenced by a deployed assembly.
        /// </summary>
        /// <param name="assemblyName">The referencing assembly.</param>
        /// <returns>The referenced assembly names.</returns>
        public ImmutableArray<string> GetReferences(string assemblyName)
            => _referencesByAssembly.TryGetValue(assemblyName, out var references)
                ? references
                : [];

        /// <summary>
        /// Finds every reference path from an assembly that reaches a name matching one of the supplied fragments.
        /// </summary>
        /// <param name="root">The assembly to walk from.</param>
        /// <param name="bannedFragments">The name fragments that constitute a violation.</param>
        /// <returns>A human-readable reference path for each violation found.</returns>
        public List<string> FindReferencePathsTo(string root, string[] bannedFragments)
        {
            var violations = new List<string>();

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { root };
            var queue = new Queue<(string Name, string Path)>();

            queue.Enqueue((root, root));

            while (queue.Count > 0)
            {
                var (current, path) = queue.Dequeue();

                foreach (var reference in GetReferences(current))
                {
                    if (!visited.Add(reference))
                    {
                        continue;
                    }

                    var referencePath = $"{path} -> {reference}";

                    if (Matches(reference, bannedFragments))
                    {
                        violations.Add(referencePath);

                        continue;
                    }

                    queue.Enqueue((reference, referencePath));
                }
            }

            return violations;
        }

        /// <summary>
        /// Finds every referenced assembly that the scan could not resolve and that is not a framework assembly.
        /// </summary>
        /// <returns>A description of each unresolved reference.</returns>
        public List<string> GetUnresolvedNonFrameworkReferences()
        {
            var unresolved = new List<string>();

            foreach (var (assembly, references) in _referencesByAssembly)
            {
                foreach (var reference in references)
                {
                    if (_referencesByAssembly.ContainsKey(reference) || IsFrameworkAssembly(reference))
                    {
                        continue;
                    }

                    unresolved.Add($"{assembly} -> {reference}");
                }
            }

            return unresolved
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Determines whether an assembly ships with the .NET runtime rather than beside the application.
        /// </summary>
        /// <param name="assemblyName">The simple assembly name to classify.</param>
        /// <returns><see langword="true"/> when the assembly is part of the shared framework.</returns>
        private static bool IsFrameworkAssembly(string assemblyName)
            => assemblyName.Equals("System", StringComparison.Ordinal) ||
                assemblyName.Equals("mscorlib", StringComparison.Ordinal) ||
                assemblyName.Equals("netstandard", StringComparison.Ordinal) ||
                assemblyName.Equals("WindowsBase", StringComparison.Ordinal) ||
                assemblyName.Equals("Microsoft.AspNetCore", StringComparison.Ordinal) ||
                assemblyName.StartsWith("System.", StringComparison.Ordinal) ||
                assemblyName.StartsWith("Microsoft.Win32.", StringComparison.Ordinal) ||
                assemblyName.StartsWith("Microsoft.CSharp", StringComparison.Ordinal) ||
                assemblyName.StartsWith("Microsoft.VisualBasic", StringComparison.Ordinal) ||
                assemblyName.StartsWith("Microsoft.AspNetCore.", StringComparison.Ordinal) ||
                assemblyName.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal) ||
                assemblyName.StartsWith("Microsoft.JSInterop", StringComparison.Ordinal) ||
                assemblyName.StartsWith("Microsoft.Net.Http.Headers", StringComparison.Ordinal);

        /// <summary>
        /// Determines whether an assembly is a test harness rather than something the product ships.
        /// </summary>
        /// <param name="assemblyName">The simple assembly name to classify.</param>
        /// <returns><see langword="true"/> when the assembly is a test harness.</returns>
        private static bool IsTestAssembly(string assemblyName)
            => assemblyName.Equals(
                typeof(ContactCenterSearchEngineIndependenceTests).Assembly.GetName().Name,
                StringComparison.Ordinal);

        /// <summary>
        /// Reads the assembly references declared by a file, if the file is a managed assembly.
        /// </summary>
        /// <param name="path">The file to read.</param>
        /// <param name="references">The referenced assembly names when the file is a managed assembly.</param>
        /// <returns><see langword="true"/> when the file is a managed assembly.</returns>
        private static bool TryReadReferences(string path, out ImmutableArray<string> references)
        {
            references = [];

            try
            {
                using var stream = File.OpenRead(path);
                using var peReader = new PEReader(stream);

                if (!peReader.HasMetadata)
                {
                    return false;
                }

                var metadata = peReader.GetMetadataReader();

                if (!metadata.IsAssembly)
                {
                    return false;
                }

                references =
                [
                    .. metadata.AssemblyReferences
                        .Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name)),
                ];

                return true;
            }
            catch (BadImageFormatException)
            {
                // A native library sitting beside the managed output, which declares no managed references.
                return false;
            }
        }
    }
}
