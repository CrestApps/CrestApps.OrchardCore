using CrestApps.Core.ContactCenter;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests.DependencyInjection;

/// <summary>
/// Records the order a tenant container hands back the service chains whose order changes behaviour,
/// and fails when that order changes.
/// </summary>
/// <remarks>
/// Several services here are consumed as a sequence and the first match wins, so reordering them
/// silently changes which one handles the work. This repository has already shipped that defect: an
/// inbound call router registered in the wrong order made calls ring without ever being offered.
/// <para>
/// This asks the container what it actually resolves rather than reading the registration list,
/// because resolution order is the thing that decides behaviour, and because a registration list
/// carries positions that Orchard Core's module discovery shuffles between runs. Each chain here is
/// contributed by a single startup, so what comes back is stable.
/// </para>
/// </remarks>
public sealed class ServiceResolutionOrderTests
{
    /// <summary>
    /// The service chains whose resolution order is load-bearing, with a feature profile that
    /// registers them.
    /// </summary>
    public static TheoryData<string, string, string[]> OrderSensitiveChains() => new()
    {
        {
            "routing-strategies",
            "CrestApps.OrchardCore.ContactCenter.Core.Services.IActivityRoutingStrategy",
            [ContactCenterConstants.Feature.Queues]
        },
        {
            "inbound-priority",
            "CrestApps.OrchardCore.ContactCenter.Core.Services.IInboundPriorityContributor",
            [ContactCenterConstants.Feature.InboundVoice]
        },
        {
            "provider-command-executors",
            "CrestApps.OrchardCore.ContactCenter.Core.Services.IProviderCommandTypeExecutor",
            // Voice is EnabledByDependencyOnly, so it has to be pulled in by something that depends
            // on it rather than enabled directly.
            [ContactCenterConstants.Feature.InboundVoice]
        },
        {
            "retention-policies",
            "CrestApps.OrchardCore.ContactCenter.Core.Services.Retention.IContactCenterRetentionPolicy",
            [ContactCenterConstants.Feature.Area]
        },
    };

    /// <summary>
    /// The chains that decide their own order, and the member that decides it.
    /// </summary>
    /// <remarks>
    /// These look like the chains above and are not. Their consumers sort or select by a value on each
    /// implementation rather than taking the sequence as given, so where a registration sits says nothing
    /// about what runs. Pinning their resolution order would fail on a harmless move and stay green through
    /// the change that actually breaks them - two implementations claiming the same value, where the loser
    /// becomes unreachable and nothing says so.
    /// <para>
    /// So what is pinned is the deciding value, and that no two members share one.
    /// </para>
    /// </remarks>
    public static TheoryData<string, string, string, string[]> SelfOrderingChains() => new()
    {
        {
            // Six routers from the portal and a seventh from routed distribution, sorted by Order before the
            // first one to claim a conversation ends the chain. A duplicate would make one of them dead, and
            // the dead one decides whether a department conversation is pushed at an agent or pooled.
            "sms-inbound-routers",
            "CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services.ISmsInboundRouter",
            "Order",
            [Omnichannel.Sms.Portal.SmsPortalConstants.Feature.Portal, Omnichannel.Sms.Portal.SmsPortalConstants.Feature.RoutedDistribution]
        },
        {
            // The pacing strategies, picked by the mode a campaign asks for. Two claiming one mode would make
            // a campaign quietly dial at the other one's pace.
            "dialer-strategies",
            "CrestApps.OrchardCore.ContactCenter.Core.Services.IDialerStrategy",
            "Mode",
            [ContactCenterConstants.Feature.DialerPaced]
        },
    };

    /// <summary>
    /// The chains where losing a member matters but running them in a different order does not, with a
    /// provider profile that registers them.
    /// </summary>
    /// <remarks>
    /// Their consumers run every member rather than stopping at the first, and their members come from
    /// more than one startup, which is an order the host does not promise to keep stable between runs.
    /// Pinning a sequence here would fail for reasons that have nothing to do with behaviour. What is
    /// pinned instead is who is in the chain, because a member that stops being registered stops doing
    /// its work and nothing reports it.
    /// </remarks>
    public static TheoryData<string, string, string> ProviderMembershipChains() => new()
    {
        {
            // Four reconcilers, each recovering a different kind of call the provider and this platform
            // have come to disagree about. Losing one leaves that kind of call stranded indefinitely.
            "asterisk-state-reconcilers",
            "CrestApps.OrchardCore.Asterisk.Services.IAsteriskProviderStateReconciler",
            "asterisk-ga-core"
        },
    };

    [Theory]
    [MemberData(nameof(OrderSensitiveChains))]
    public Task OrderSensitiveChains_ResolveInTheApprovedOrder(string chainId, string serviceTypeName, string[] features)
        => AssertResolutionOrderAsync(chainId, serviceTypeName, "none", features);

    [Theory]
    [MemberData(nameof(ProviderMembershipChains))]
    public void ProviderMembershipChains_AreTrulyOrderInsensitive(string chainId, string serviceTypeName, string providerProfile)
    {
        // Guards the claim this category rests on. If one of these chains grows a consumer that stops at the
        // first match, its order starts deciding behaviour while nothing pins that order - and the host does
        // not promise to keep it stable. Move the chain to OrderSensitiveChains if this ever fires.
        var consumers = FindConsumers(serviceTypeName);

        Assert.True(
            consumers.Count > 0,
            $"Nothing consumes '{serviceTypeName}', so '{chainId}' (pinned against the '{providerProfile}' profile) " +
            "proves nothing. Remove it or fix the name.");

        // Deliberately only the selection pattern. The loop does carry a break, for cancellation, and reading
        // that as an early exit would make this fire on code that is fine.
        Assert.All(consumers, source => Assert.DoesNotContain("FirstOrDefault", source, StringComparison.Ordinal));
    }

    /// <summary>
    /// Finds the source of everything that takes a chain as a sequence.
    /// </summary>
    /// <param name="serviceTypeName">The full name of the chain's contract.</param>
    /// <returns>The text of each file that injects the sequence.</returns>
    private static List<string> FindConsumers(string serviceTypeName)
    {
        var shortName = serviceTypeName[(serviceTypeName.LastIndexOf('.') + 1)..];
        var needle = $"IEnumerable<{shortName}>";

        return
        [
            .. Directory
                .EnumerateFiles(Path.Combine(FindRepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Select(File.ReadAllText)
                .Where(source => source.Contains(needle, StringComparison.Ordinal)),
        ];
    }

    /// <summary>
    /// Walks up from the test binaries to the repository the sources live in.
    /// </summary>
    /// <returns>The repository root.</returns>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.True(directory is not null, "The repository root was not found, so this gate reads nothing.");

        return directory.FullName;
    }

    [Theory]
    [MemberData(nameof(ProviderMembershipChains))]
    public async Task ProviderMembershipChains_HaveTheApprovedMembers(string chainId, string serviceTypeName, string providerProfile)
    {
        var matrix = await ContactCenterSupportMatrix.LoadAsync();
        var profile = matrix.TenantProfiles
            .First(candidate => string.Equals(candidate.ProviderProfile, providerProfile, StringComparison.Ordinal));

        await AssertResolutionOrderAsync(chainId, serviceTypeName, providerProfile, [.. profile.Features], sorted: true);
    }

    [Theory]
    [MemberData(nameof(SelfOrderingChains))]
    public async Task SelfOrderingChains_LeaveNoMemberUnreachable(
        string chainId,
        string serviceTypeName,
        string discriminatorName,
        string[] features)
    {
        // Arrange
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var tenant = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = chainId,
            ProviderProfile = "none",
            Features = features,
        });

        var serviceType = FindServiceType(serviceTypeName);

        Assert.True(
            serviceType is not null,
            $"'{serviceTypeName}' was not found in any loaded assembly, so this gate proves nothing.");

        // Act
        var members = await host.ExecuteInTenantScopeAsync(tenant, serviceProvider =>
        {
            var sequenceType = typeof(IEnumerable<>).MakeGenericType(serviceType);
            var services = (System.Collections.IEnumerable)serviceProvider.GetService(sequenceType);
            var discriminator = serviceType.GetProperty(discriminatorName);

            var described = new List<string>();

            foreach (var service in services ?? Array.Empty<object>())
            {
                described.Add($"{service.GetType().FullName} = {discriminator.GetValue(service)}");
            }

            return Task.FromResult(described);
        });

        // Assert
        Assert.True(
            members.Count > 0,
            $"The tenant resolved no implementation of '{serviceTypeName}', so this gate proves nothing.");

        var duplicates = members
            .GroupBy(member => member[(member.LastIndexOf(" = ", StringComparison.Ordinal) + 3)..], StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{discriminatorName} {group.Key}: {string.Join(", ", group)}")
            .ToList();

        Assert.True(
            duplicates.Count == 0,
            $"Two members of '{chainId}' claim the same {discriminatorName}, so one of them never runs and " +
            $"nothing reports it: {string.Join("; ", duplicates)}.");

        // Sorted by type rather than by the deciding value, so the file cannot be misread as a run order.
        // What it records is which implementation claims which value.
        await AssertMatchesBaselineAsync(
            chainId,
            string.Join(Environment.NewLine, members.Order(StringComparer.Ordinal)) + Environment.NewLine);
    }

    private static async Task AssertResolutionOrderAsync(
        string chainId,
        string serviceTypeName,
        string providerProfile,
        string[] features,
        bool sorted = false)
    {
        // Arrange
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();

        var tenant = await host.CreateTenantAsync(new ContactCenterTenantProfile
        {
            Id = chainId,
            ProviderProfile = providerProfile,
            Features = features,
        });

        // Looked up only after the host has started. Module assemblies load lazily, and these cases
        // run in parallel, so a lookup before the host is up finds nothing for reasons that have
        // nothing to do with the contract still existing.
        var serviceType = FindServiceType(serviceTypeName);

        Assert.True(
            serviceType is not null,
            $"'{serviceTypeName}' was not found in any loaded assembly, so this gate proves nothing. If the type " +
            "was renamed or moved, update this chain; if it was deleted, remove it.");

        // Act
        var resolved = await host.ExecuteInTenantScopeAsync(tenant, serviceProvider =>
        {
            var sequenceType = typeof(IEnumerable<>).MakeGenericType(serviceType);
            var services = (System.Collections.IEnumerable)serviceProvider.GetService(sequenceType);

            var names = new List<string>();

            foreach (var service in services ?? Array.Empty<object>())
            {
                names.Add(service.GetType().FullName);
            }

            return Task.FromResult(names);
        });

        // Assert
        Assert.True(
            resolved.Count > 0,
            $"The tenant resolved no implementation of '{serviceTypeName}', so its order cannot be pinned. Either " +
            "the feature that registers the chain is missing from this entry, or the chain no longer exists.");

        await AssertMatchesBaselineAsync(
            chainId,
            string.Join(Environment.NewLine, sorted ? resolved.Order(StringComparer.Ordinal) : resolved) + Environment.NewLine);
    }

    /// <summary>
    /// Compares what a chain resolved to against what was approved, writing the first one for review.
    /// </summary>
    /// <param name="chainId">The chain identifier, which names its baseline.</param>
    /// <param name="actual">What the tenant resolved.</param>
    private static async Task AssertMatchesBaselineAsync(string chainId, string actual)
    {
        var baselinePath = GetBaselinePath(chainId);

        if (!File.Exists(baselinePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(baselinePath));
            await File.WriteAllTextAsync(baselinePath, actual, TestContext.Current.CancellationToken);

            Assert.Fail(
                $"No approved resolution order existed for '{chainId}'. One has been written to '{baselinePath}'. " +
                "Review the order and commit it.");
        }

        var approved = await File.ReadAllTextAsync(baselinePath, TestContext.Current.CancellationToken);

        Assert.True(
            string.Equals(Normalize(approved), Normalize(actual), StringComparison.Ordinal),
            Describe(chainId, Normalize(approved), Normalize(actual)));
    }

    /// <summary>
    /// Finds a service type by name across every loaded assembly.
    /// </summary>
    /// <remarks>
    /// By name rather than by reference, so this test project does not have to reference a module
    /// assembly just to name the contract it is pinning.
    /// </remarks>
    /// <param name="fullName">The full type name.</param>
    /// <returns>The type, or <see langword="null"/> when nothing matches.</returns>
    private static Type FindServiceType(string fullName)
        => AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name?.StartsWith("CrestApps.", StringComparison.Ordinal) == true)
            .SelectMany(GetLoadableTypes)
            .FirstOrDefault(type => string.Equals(type.FullName, fullName, StringComparison.Ordinal));

    /// <summary>
    /// Gets the types an assembly exposes, tolerating types whose dependencies cannot be loaded.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns>The types that could be loaded.</returns>
    private static Type[] GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type is not null).ToArray();
        }
    }

    /// <summary>
    /// Gets the directory the baselines live in, from the repository rather than the output folder.
    /// </summary>
    /// <returns>The baseline directory.</returns>
    private static string GetBaselineDirectory()
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
            "DependencyInjection",
            "Baselines");
    }

    /// <summary>
    /// Gets the baseline path for a chain.
    /// </summary>
    /// <param name="chainId">The chain id.</param>
    /// <returns>The baseline path.</returns>
    private static string GetBaselinePath(string chainId)
        => Path.Combine(GetBaselineDirectory(), $"order-{chainId}.approved.txt");

    /// <summary>
    /// Normalizes line endings so a baseline compares equal across platforms.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns>The normalized text.</returns>
    private static string Normalize(string value)
        => value.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');

    /// <summary>
    /// Builds an assertion message that shows both orders.
    /// </summary>
    /// <param name="chainId">The chain id.</param>
    /// <param name="approved">The approved order.</param>
    /// <param name="actual">The resolved order.</param>
    /// <returns>The assertion message.</returns>
    private static string Describe(string chainId, string approved, string actual)
        => new StringBuilder()
            .Append("The tenant resolves '")
            .Append(chainId)
            .AppendLine("' in a different order than approved. The first match in these chains wins, so this")
            .AppendLine("changes which implementation handles the work.")
            .AppendLine()
            .AppendLine("Approved:")
            .AppendLine(approved)
            .AppendLine()
            .AppendLine("Now:")
            .AppendLine(actual)
            .ToString();
}
