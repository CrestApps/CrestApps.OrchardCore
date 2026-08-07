using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Guards the rule that a Contact Center aggregate decides its own status changes.
/// <para>
/// These aggregates used to be plain property bags: every status was publicly settable, so the question "can this
/// call move from planned to held" was answered separately by the ingestion service, the command executors, the
/// healing service and the router, and each answer could differ. The only ordering rule that existed grouped
/// states into phases, which cannot tell an illegal edge from a legal one inside a phase — a call recorded as
/// held that was never answered does not regress, so nothing refused it, and every duration and wallboard built
/// on that record reported the fiction as real. Closing the setters moves the answer into the aggregate, where
/// there is exactly one of it. This guard keeps it there: a reopened setter, or a production call to the test-only
/// restore seam, puts the old shape back and would otherwise go unnoticed until it produced wrong numbers.
/// </para>
/// </summary>
public sealed class AggregateLifecycleArchitectureTests
{
    private static readonly (Type Aggregate, string Property)[] _guardedStatuses =
    [
        (typeof(Interaction), nameof(Interaction.Status)),
        (typeof(CallSession), nameof(CallSession.State)),
        (typeof(QueueItem), nameof(QueueItem.Status)),
        (typeof(ActivityReservation), nameof(ActivityReservation.Status)),
        (typeof(ContactCenterWorkState), nameof(ContactCenterWorkState.AssignmentStatus)),
    ];

    private static readonly string[] _guardedRoots =
    [
        "src/Core/CrestApps.OrchardCore.ContactCenter.Core",
        "src/Modules/CrestApps.OrchardCore.ContactCenter",
        "src/Modules/CrestApps.OrchardCore.Telephony",
        "src/Modules/CrestApps.OrchardCore.Asterisk",
        "src/Modules/CrestApps.OrchardCore.DialPad",
    ];

    private static readonly Regex _bypassRegex = new(
        @"\.\s*(?:MirrorSessionStatus|MirrorProviderState|AdoptActivityAssignmentStatus)\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex _restoreSeamRegex = new(
        @"\.\s*RestorePersisted(?:Status|State|AssignmentStatus)\s*\(",
        RegexOptions.Compiled);

    [Fact]
    public void EveryGuardedAggregateStatus_HasNoPublicSetter()
    {
        // Arrange
        var violations = new List<string>();

        // Act
        foreach (var (aggregate, propertyName) in _guardedStatuses)
        {
            var property = aggregate.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

            Assert.NotNull(property);

            if (property.SetMethod is not null && property.SetMethod.IsPublic)
            {
                violations.Add($"{aggregate.Name}.{propertyName} has a public setter.");
            }
        }

        // Assert
        // A public setter is all it takes for the next caller to answer the transition question for itself, which
        // is the shape that let a call be recorded as held without ever having been answered.
        Assert.Empty(violations);
    }

    [Fact]
    public void EveryGuardedAggregateStatus_SurvivesASerializationRoundTrip()
    {
        // A private setter is dropped by System.Text.Json unless the property is explicitly included, and the
        // failure is silent: every persisted aggregate would come back holding the enum's default. That reads as
        // a created interaction, a planned call and a waiting queue item, so live work would look like new work.
        var interaction = new Interaction();
        interaction.TransitionTo(InteractionStatus.Ringing);
        interaction.TransitionTo(InteractionStatus.Connected);

        var session = new CallSession();
        session.TransitionTo(VoiceCallState.Ringing);
        session.TransitionTo(VoiceCallState.Connected);

        var item = new QueueItem();
        item.TransitionTo(QueueItemStatus.Reserved);

        var reservation = new ActivityReservation();
        reservation.TransitionTo(ReservationStatus.Accepted);

        var workState = new ContactCenterWorkState();
        workState.TransitionTo(ActivityAssignmentStatus.Assigned);

        Assert.Equal(InteractionStatus.Connected, RoundTrip(interaction).Status);
        Assert.Equal(VoiceCallState.Connected, RoundTrip(session).State);
        Assert.Equal(QueueItemStatus.Reserved, RoundTrip(item).Status);
        Assert.Equal(ReservationStatus.Accepted, RoundTrip(reservation).Status);
        Assert.Equal(ActivityAssignmentStatus.Assigned, RoundTrip(workState).AssignmentStatus);
    }

    [Fact]
    public void NoProductionCode_UsesTheTestOnlyRestoreSeam()
    {
        // Arrange
        var violations = new List<string>();

        // Act
        foreach (var file in EnumerateGuardedFiles())
        {
            var lines = File.ReadAllLines(file.FullPath);

            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart();

                if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("///", StringComparison.Ordinal))
                {
                    continue;
                }

                if (_restoreSeamRegex.IsMatch(lines[i]))
                {
                    violations.Add($"{file.RelativePath}({i + 1}): {trimmed}");
                }
            }
        }

        // Assert
        // The restore seam sets a status without asking whether the move exists. It is there so a test can arrange
        // a state directly; the moment production code reaches for it, the lifecycle stops being enforced on that
        // path and the aggregate is a property bag again for everyone downstream of it.
        Assert.Empty(violations);
    }

    [Fact]
    public void TheGuardedRoots_Exist()
    {
        var repositoryRoot = FindRepositoryRoot();

        foreach (var root in _guardedRoots)
        {
            Assert.True(
                Directory.Exists(Path.Combine(repositoryRoot, root.Replace('/', Path.DirectorySeparatorChar))),
                $"The guarded root '{root}' no longer exists. Update {nameof(AggregateLifecycleArchitectureTests)} so the rule keeps covering the code that mutates these aggregates.");
        }
    }

    [Fact]
    public void TheGuard_ScansTheFilesItClaimsTo()
    {
        // A guard that quietly scans nothing because a file moved is worse than no guard, so the files that
        // carried the original divergent status writes are named here.
        var files = EnumerateGuardedFiles();

        Assert.Contains(files, file => file.RelativePath.EndsWith("Services/ProviderVoiceEventService.cs", StringComparison.Ordinal));
        Assert.Contains(files, file => file.RelativePath.EndsWith("Services/ActivityReservationService.cs", StringComparison.Ordinal));
        Assert.Contains(files, file => file.RelativePath.EndsWith("Services/AgentWorkStateHealingService.cs", StringComparison.Ordinal));
        Assert.Contains(files, file => file.RelativePath.EndsWith("Services/InboundVoiceCallProcessor.cs", StringComparison.Ordinal));
        Assert.Contains(files, file => file.RelativePath.EndsWith("Services/ContactCenterWorkStateProjector.cs", StringComparison.Ordinal));
    }

    private static T RoundTrip<T>(T value)
        => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value));

    [Fact]
    public void EveryLifecycleBypass_IsCalledOnlyFromItsDeclaredProviderProjection()
    {
        // Arrange
        // Three methods write a status without consulting the lifecycle table, because on these paths the
        // provider has already decided what happened and re-deciding it here would let two records disagree.
        // That reasoning holds only where the caller really is projecting a provider or activity fact, so the
        // set of callers is pinned: a fourth file reaching for one of these is a caller that wanted to skip a
        // refusal, which is exactly the property-bag behaviour the guarded aggregates removed.
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            "src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/ProviderVoiceEventService.cs",
            "src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/ContactCenterWorkStateProjector.cs",
        };

        var callers = new SortedSet<string>(StringComparer.Ordinal);
        var callCount = 0;

        // Act
        foreach (var (fullPath, relativePath) in EnumerateGuardedFiles())
        {
            var matches = _bypassRegex.Matches(File.ReadAllText(fullPath));

            if (matches.Count == 0)
            {
                continue;
            }

            callers.Add(relativePath);
            callCount += matches.Count;
        }

        // Assert
        Assert.Equal(expected.OrderBy(path => path, StringComparer.Ordinal), callers);
        Assert.Equal(4, callCount);
    }

    private static List<(string FullPath, string RelativePath)> EnumerateGuardedFiles()
    {
        var repositoryRoot = FindRepositoryRoot();
        var files = new List<(string FullPath, string RelativePath)>();

        foreach (var root in _guardedRoots)
        {
            var directory = Path.Combine(repositoryRoot, root.Replace('/', Path.DirectorySeparatorChar));

            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(repositoryRoot, file).Replace(Path.DirectorySeparatorChar, '/');

                if (relativePath.Contains("/obj/", StringComparison.Ordinal) || relativePath.Contains("/bin/", StringComparison.Ordinal))
                {
                    continue;
                }

                files.Add((file, relativePath));
            }
        }

        return files;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src"))
                && Directory.Exists(Path.Combine(directory.FullName, "tests", "CrestApps.OrchardCore.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root from the test assembly location.");
    }
}
