using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telephony.Core.Models;
using CrestApps.OrchardCore.Telephony.Core.Services;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Guards the layering the voice ingress depends on. Two stacks consume the same provider stream: the
/// soft-phone telephony projection and the Contact Center projection. While the mechanics that make a
/// provider stream safe to consume — canonical provider identity, the ingestion lock key, the delivery
/// idempotency key, and the ordering rules that decide which deliveries are stale — lived inside the
/// Contact Center, only one of the two stacks could have them, and telephony could not ingest at all
/// with Contact Center disabled.
/// </summary>
public sealed partial class VoiceIngressLayeringArchitectureTests
{
    private static readonly string[] _contactCenterAssemblyPrefixes =
    [
        "CrestApps.OrchardCore.ContactCenter",
        "CrestApps.OrchardCore.Omnichannel",
    ];

    [Fact]
    public void TelephonyCore_ReferencesNoContactCenterAssembly()
    {
        // Arrange: the compiled assembly reference list is checked as well as the declared project
        // closure, because the compiler omits a reference the assembly never actually uses. Checking
        // only the compiled list would let a project reference be added today and used tomorrow.
        var telephonyCore = typeof(VoiceIngressKeys).Assembly;

        // Act
        var referenced = telephonyCore
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .ToArray();

        var violations = referenced
            .Where(IsContactCenterName)
            .ToArray();

        // Assert
        Assert.Empty(violations);
        Assert.NotEmpty(referenced);
    }

    [Fact]
    public void TelephonyCore_DeclaresNoContactCenterProjectReference()
    {
        // Arrange
        var project = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Core",
            "CrestApps.OrchardCore.Telephony.Core",
            "CrestApps.OrchardCore.Telephony.Core.csproj");

        // Act
        var closure = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        CollectProjectClosure(project, closure);

        var violations = closure
            .Keys
            .Where(IsContactCenterName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Assert
        Assert.Empty(violations);
        Assert.True(closure.Count > 1, $"The project closure resolved only {closure.Count} projects, which is too few to be meaningful.");
    }

    [Fact]
    public void TheProjectClosureWalker_DetectsAContactCenterReference()
    {
        // Arrange: without this the closure test would pass vacuously if the walker silently resolved
        // nothing, so it is pointed at a project that really does reference the Contact Center.
        var project = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "Core",
            "CrestApps.OrchardCore.ContactCenter.Core",
            "CrestApps.OrchardCore.ContactCenter.Core.csproj");

        // Act
        var closure = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        CollectProjectClosure(project, closure);

        // Assert
        Assert.Contains(closure.Keys, IsContactCenterName);
        Assert.Contains("CrestApps.OrchardCore.Telephony.Core", closure.Keys);
    }

    [Fact]
    public void TelephonyCore_ExposesTheProviderNeutralIngressMechanics()
    {
        // Arrange: without this the emptiness assertion above would pass for an empty assembly, and the
        // layering would be "proven" by a project that carries none of the mechanics it was created for.
        var telephonyCore = typeof(VoiceIngressKeys).Assembly;

        // Act
        var publicTypes = telephonyCore
            .GetExportedTypes()
            .Select(type => type.FullName)
            .ToArray();

        // Assert
        Assert.Contains("CrestApps.OrchardCore.Telephony.Core.Services.IProviderIdentityResolver", publicTypes);
        Assert.Contains("CrestApps.OrchardCore.Telephony.Core.Services.ProviderIdentityResolver", publicTypes);
        Assert.Contains("CrestApps.OrchardCore.Telephony.Core.Services.VoiceIngressKeys", publicTypes);
        Assert.Contains("CrestApps.OrchardCore.Telephony.Core.Services.VoiceStreamOrdering", publicTypes);
        Assert.Contains("CrestApps.OrchardCore.Telephony.Core.Models.VoiceCallLifecyclePhase", publicTypes);
    }

    [Fact]
    public void ContactCenterCore_DoesNotRedeclareTheIngressMechanics()
    {
        // Arrange: the mechanics must be delegated, not copied. A copy compiles, passes every existing
        // test, and silently diverges the moment one side is changed.
        var contactCenterCore = typeof(ProviderVoiceEventService).Assembly;
        var forbidden = new[]
        {
            "ProviderIdentityResolver",
            "VoiceIngressKeys",
            "VoiceStreamOrdering",
        };

        // Act
        var declared = contactCenterCore
            .GetTypes()
            .Select(type => type.Name)
            .ToArray();

        var violations = forbidden
            .Where(name => Array.Exists(declared, declaredName => string.Equals(declaredName, name, StringComparison.Ordinal)))
            .ToArray();

        // Assert
        Assert.Empty(violations);
    }

    [Fact]
    public void ContactCenterIngestion_UsesTheSharedIngressKeys()
    {
        // Arrange: the Contact Center's own provider-event key must be exactly the shared computation,
        // because the telephony projection derives its dedupe key the same way. If the two ever differ,
        // the same delivery is de-duplicated in one projection and applied in the other.
        const string providerName = "Asterisk";
        const string rawKey = "1720000000.42";

        // Act
        var contactCenterKey = ContactCenterClaimKeys.BuildProviderEventIdempotencyKey(providerName, rawKey);
        var sharedKey = VoiceIngressKeys.BuildEventIdempotencyKey(providerName, rawKey);

        // Assert
        Assert.Equal(sharedKey, contactCenterKey);
    }

    [Theory]
    [InlineData("Asterisk", "1720000000.42")]
    [InlineData("DialPad", "1720000000.42")]
    [InlineData("Asterisk", "webhook-delivery-9f2c")]
    public void EventIdempotencyKey_IsStable(string providerName, string rawKey)
    {
        // Arrange: the key is persisted, so its computation is a compatibility surface. Recomputing it
        // here from the documented algorithm proves an upgraded node keys deliveries exactly as a
        // previous-version node did, which a self-referential assertion could never prove.
        var expected = "provider-event:v1:" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{providerName}\n{rawKey}")));

        // Act
        var actual = VoiceIngressKeys.BuildEventIdempotencyKey(providerName, rawKey);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EventIdempotencyKey_IsScopedByProvider()
    {
        // Act
        var asterisk = VoiceIngressKeys.BuildEventIdempotencyKey("Asterisk", "shared-raw-id");
        var dialPad = VoiceIngressKeys.BuildEventIdempotencyKey("DialPad", "shared-raw-id");

        // Assert
        Assert.NotEqual(asterisk, dialPad);
    }

    [Fact]
    public void IngestionLockKey_IsStableAndScopedByProviderCall()
    {
        // Arrange
        var expected = "ContactCenterProviderVoiceEvent:" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes("Asterisk\ncall-1")));

        // Act
        var actual = VoiceIngressKeys.BuildIngestionLockKey("Asterisk", "call-1");
        var otherCall = VoiceIngressKeys.BuildIngestionLockKey("Asterisk", "call-2");
        var otherProvider = VoiceIngressKeys.BuildIngestionLockKey("DialPad", "call-1");

        // Assert
        Assert.Equal(expected, actual);
        Assert.NotEqual(actual, otherCall);
        Assert.NotEqual(actual, otherProvider);
    }

    [Fact]
    public void ContactCenterIngestion_DelegatesItsStalenessDecisionToTheSharedRules()
    {
        // Arrange: the Contact Center must not keep a private copy of the ordering rules, so the private
        // staleness helper is required to route through the shared implementation. Reflection is used
        // because the helper is deliberately private; the assertion is that no lifecycle-rank table
        // survives alongside the shared one.
        var declared = typeof(ProviderVoiceEventService)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Select(method => method.Name)
            .ToArray();

        // Assert
        Assert.DoesNotContain("GetLifecycleRank", declared);
        Assert.Contains("GetLifecyclePhase", declared);
        Assert.Contains("ShouldIgnoreEvent", declared);
    }

    [Theory]
    [InlineData(VoiceCallLifecyclePhase.Established, VoiceCallLifecyclePhase.Alerting, true)]
    [InlineData(VoiceCallLifecyclePhase.Alerting, VoiceCallLifecyclePhase.Established, false)]
    [InlineData(VoiceCallLifecyclePhase.Terminal, VoiceCallLifecyclePhase.Terminal, true)]
    [InlineData(VoiceCallLifecyclePhase.Established, VoiceCallLifecyclePhase.Terminal, false)]
    public void SharedOrdering_RejectsLifecycleRegressions(
        VoiceCallLifecyclePhase current,
        VoiceCallLifecyclePhase incoming,
        bool expectedDiscard)
    {
        // Arrange
        var watermark = new VoiceStreamWatermark
        {
            Phase = current,
        };

        var delivery = new VoiceStreamDelivery
        {
            Phase = incoming,
            OccurredUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        // Act
        var discarded = VoiceStreamOrdering.ShouldDiscard(watermark, delivery);

        // Assert
        Assert.Equal(expectedDiscard, discarded);
    }

    [Fact]
    public void SharedOrdering_RejectsUnsequencedDeliveryOnceASequenceDomainExists()
    {
        // Arrange
        var watermark = new VoiceStreamWatermark
        {
            Phase = VoiceCallLifecyclePhase.Alerting,
            HighWaterSequence = 7,
        };

        // Act
        var unsequenced = VoiceStreamOrdering.ShouldDiscard(
            watermark,
            new VoiceStreamDelivery
            {
                Phase = VoiceCallLifecyclePhase.Established,
                OccurredUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            });

        var replayed = VoiceStreamOrdering.ShouldDiscard(
            watermark,
            new VoiceStreamDelivery
            {
                Phase = VoiceCallLifecyclePhase.Established,
                SequenceNumber = 7,
                OccurredUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            });

        var advancing = VoiceStreamOrdering.ShouldDiscard(
            watermark,
            new VoiceStreamDelivery
            {
                Phase = VoiceCallLifecyclePhase.Established,
                SequenceNumber = 8,
                OccurredUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            });

        // Assert
        Assert.True(unsequenced);
        Assert.True(replayed);
        Assert.False(advancing);
    }

    [Fact]
    public void SharedOrdering_NeverDiscardsATerminalDelivery()
    {
        // Arrange: a hangup can carry a timestamp behind the state change that preceded it and can arrive
        // unsequenced after a sequenced delivery. Discarding it strands the stream live forever.
        var watermark = new VoiceStreamWatermark
        {
            Phase = VoiceCallLifecyclePhase.Established,
            HighWaterSequence = 99,
            LastEventUtc = new DateTime(2026, 1, 1, 0, 5, 0, DateTimeKind.Utc),
        };

        var delivery = new VoiceStreamDelivery
        {
            Phase = VoiceCallLifecyclePhase.Terminal,
            OccurredUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        // Act
        var discarded = VoiceStreamOrdering.ShouldDiscard(watermark, delivery);

        // Assert
        Assert.False(discarded);
    }

    [Fact]
    public void SharedOrdering_AdvancesTheWatermarkMonotonically()
    {
        // Arrange
        var watermark = new VoiceStreamWatermark
        {
            Phase = VoiceCallLifecyclePhase.Established,
            HighWaterSequence = 10,
            LastEventUtc = new DateTime(2026, 1, 1, 0, 5, 0, DateTimeKind.Utc),
        };

        // Act
        VoiceStreamOrdering.Advance(
            watermark,
            new VoiceStreamDelivery
            {
                Phase = VoiceCallLifecyclePhase.Terminal,
                SequenceNumber = 4,
                OccurredUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            });

        // Assert
        Assert.Equal(VoiceCallLifecyclePhase.Terminal, watermark.Phase);
        Assert.Equal(10, watermark.HighWaterSequence);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 5, 0, DateTimeKind.Utc), watermark.LastEventUtc);
    }

    private static bool IsContactCenterName(string name)
        => Array.Exists(_contactCenterAssemblyPrefixes, prefix => name.StartsWith(prefix, StringComparison.Ordinal));

    private static void CollectProjectClosure(string projectPath, Dictionary<string, string> closure)
    {
        var name = Path.GetFileNameWithoutExtension(projectPath);

        if (!closure.TryAdd(name, projectPath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(projectPath);

        foreach (Match match in ProjectReference().Matches(File.ReadAllText(projectPath)))
        {
            var relative = match.Groups["path"].Value.Replace('\\', Path.DirectorySeparatorChar);
            var resolved = Path.GetFullPath(Path.Combine(directory, relative));

            if (File.Exists(resolved))
            {
                CollectProjectClosure(resolved, closure);
            }
        }
    }

    [GeneratedRegex("<ProjectReference\\s+Include=\"(?<path>[^\"]+)\"")]
    private static partial Regex ProjectReference();

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new InvalidOperationException("The repository root could not be located.");
    }
}
