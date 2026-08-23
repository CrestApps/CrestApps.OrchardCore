using System.Text.RegularExpressions;
using CrestApps.OrchardCore.SignalR.Core;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Pins the hub cancellation convention. Work whose only product is a value returned to the calling connection may
/// honour the connection's own token, because if the caller is gone the answer has nowhere to go. Work that changes
/// SignalR group membership may not: abandoning it part-way leaves the connection subscribed to some groups and not
/// others, and nothing later repairs that.
/// <para>
/// The scan looks for the group membership calls themselves rather than trying to identify hub classes first. A hub
/// is not required to be named for what it is, nor to derive from <c>Hub</c> directly — <c>ChatHubBase</c> matches
/// no <c>*Hub.cs</c> glob, and <c>AIChatHub</c> derives from <c>AIChatHubCore</c> — so any rule that enumerates
/// hubs before enumerating their calls has a blind spot. Enumerating the calls has none, and it also reaches group
/// changes made outside a hub class, such as the outbox notifier's.
/// </para>
/// </summary>
public sealed partial class HubCancellationConventionTests
{
    [Fact]
    public void NoGroupMembershipChange_IsCancellableByTheConnectionToken()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var callSites = FindGroupMembershipCallSites(repositoryRoot);

        // Act
        var violations = callSites
            .Where(callSite => callSite.Arguments.Contains("ConnectionAborted", StringComparison.Ordinal))
            .Select(callSite => $"{callSite.RelativePath}:{callSite.LineNumber}");

        // Assert
        Assert.True(
            !violations.Any(),
            "Group membership changes must not be cancellable by the connection's own token, because a " +
            "half-applied membership change leaves the connection subscribed to some groups and not others and " +
            "nothing later repairs it. Use HubConnectionWork.MustComplete instead. Violations: " +
            string.Join(", ", violations));
    }

    [Fact]
    public void TheScan_ReachesEveryFileThatChangesGroupMembership()
    {
        // The convention test reports no violations when it finds nothing at all, so what the scan actually reaches
        // has to be asserted separately or a broken scan would be indistinguishable from a passing gate.

        // Arrange
        var repositoryRoot = FindRepositoryRoot();

        // Act
        var files = FindGroupMembershipCallSites(repositoryRoot)
            .Select(callSite => Path.GetFileName(callSite.RelativePath))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        // Assert
        Assert.Equal(
            [
                "ContactCenterHub.cs",
                "ContactCenterRealTimeNotifier.cs",
                "ContactCenterRealTimeNotifierTests.cs",
                "DistributedTestHub.cs",
                "SmsPortalHub.cs",
                "TelephonyHub.cs",
            ],
            files);
    }

    [Fact]
    public void TheConventionToken_IsOneThatNeverCancels()
    {
        // Act & Assert
        Assert.False(HubConnectionWork.MustComplete.CanBeCanceled);
    }

    private static List<GroupMembershipCallSite> FindGroupMembershipCallSites(string repositoryRoot)
    {
        var callSites = new List<GroupMembershipCallSite>();

        var sourceFiles = new[] { "src", "tests" }
            .SelectMany(area => Directory.GetFiles(Path.Combine(repositoryRoot, area), "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        foreach (var sourceFile in sourceFiles)
        {
            var source = File.ReadAllText(sourceFile);

            if (!source.Contains("GroupAsync", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (Match match in GroupMembershipCallRegex().Matches(source))
            {
                // The argument list is read by balancing parentheses rather than by a regex, because these calls
                // routinely nest another call in an argument and a pattern that stopped at the first closing
                // parenthesis would never see the token argument at all.
                callSites.Add(new GroupMembershipCallSite
                {
                    RelativePath = Path.GetRelativePath(repositoryRoot, sourceFile),
                    LineNumber = source.Take(match.Index).Count(character => character == '\n') + 1,
                    Arguments = ReadArgumentList(source, match.Index + match.Length - 1),
                });
            }
        }

        return callSites;
    }

    private static string ReadArgumentList(string source, int openParenthesisIndex)
    {
        var depth = 0;

        for (var i = openParenthesisIndex; i < source.Length; i++)
        {
            if (source[i] == '(')
            {
                depth++;
            }
            else if (source[i] == ')')
            {
                depth--;

                if (depth == 0)
                {
                    return source.Substring(openParenthesisIndex, i - openParenthesisIndex + 1);
                }
            }
        }

        throw new InvalidOperationException("A group membership call has an unbalanced argument list.");
    }

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

    [GeneratedRegex(@"\.(AddToGroupAsync|RemoveFromGroupAsync)\s*\(")]
    private static partial Regex GroupMembershipCallRegex();

    private sealed class GroupMembershipCallSite
    {
        public string RelativePath { get; init; }

        public int LineNumber { get; init; }

        public string Arguments { get; init; }
    }
}
