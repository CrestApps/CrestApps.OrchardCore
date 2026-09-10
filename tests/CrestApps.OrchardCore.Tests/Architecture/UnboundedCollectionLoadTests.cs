using System.Text;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Some collections are bounded by how much an administrator configures — queues, dispositions, skills, entry
/// points. Loading all of those is fine, and the deployment sources have to. Others grow by one row per
/// customer interaction: activities, SMS conversations, telephony interactions, messages. Loading all of
/// <em>those</em> works perfectly in every test and on every new tenant, and then one day a tenant with two
/// years of history pulls its whole call history into memory to answer a single request.
/// <para>
/// The unbounded loads are not there today. This is what keeps it that way, because the call that introduces one
/// looks exactly like the calls above it and nothing else in the repository would object.
/// </para>
/// </summary>
public sealed partial class UnboundedCollectionLoadTests
{
    /// <summary>
    /// The stores whose contents grow with customer traffic rather than with configuration.
    /// </summary>
    private static readonly string[] _highVolumeStores =
    [
        "activityStore",
        "_activityStore",
        "activityManager",
        "_activityManager",
        "conversationStore",
        "_conversationStore",
        "interactionStore",
        "_interactionStore",
        "interactionManager",
        "_interactionManager",
        "messageStore",
        "_messageStore",
        "reservationStore",
        "_reservationStore",
    ];

    private static readonly string[] _scannedProjects =
    [
        Path.Combine("Core", "CrestApps.OrchardCore.ContactCenter.Core"),
        Path.Combine("Modules", "CrestApps.OrchardCore.ContactCenter"),
        Path.Combine("Core", "CrestApps.OrchardCore.Omnichannel.Core"),
        Path.Combine("Modules", "CrestApps.OrchardCore.Omnichannel.Managements"),
        Path.Combine("Core", "CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core"),
        Path.Combine("Modules", "CrestApps.OrchardCore.Omnichannel.Sms.Portal"),
        Path.Combine("Core", "CrestApps.OrchardCore.Omnichannel.Voice.Core"),
        Path.Combine("Modules", "CrestApps.OrchardCore.Telephony"),
        Path.Combine("Core", "CrestApps.OrchardCore.Telnyx.Core"),
        Path.Combine("Modules", "CrestApps.OrchardCore.Telnyx"),
    ];

    [Fact]
    public void NoHighVolumeCollection_IsLoadedInFull()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var violations = new List<string>();
        var pattern = GetAllPattern();

        // Act
        foreach (var (relativePath, lineNumber, line) in EnumerateLines(repositoryRoot))
        {
            var match = pattern.Match(line);

            if (!match.Success)
            {
                continue;
            }

            var receiver = match.Groups["receiver"].Value;

            if (!_highVolumeStores.Contains(receiver, StringComparer.Ordinal))
            {
                continue;
            }

            violations.Add(
                $"{relativePath}:{lineNumber} loads every row of a collection that grows with customer traffic ('{receiver}'). " +
                "Page it, or count it in the database, so the cost does not grow with the tenant's history.");
        }

        // Assert
        Assert.True(violations.Count == 0, BuildMessage(violations));
    }

    [Theory]
    [InlineData("        var all = await _activityStore.GetAllAsync();", true)]
    [InlineData("            foreach (var thread in await conversationStore.GetAllAsync())", true)]
    [InlineData("        var queues = await _queueManager.GetAllAsync();", false)]
    [InlineData("        var entries = await _manager.GetAllAsync();", false)]
    public void TheRule_RecognisesTheCallItIsLookingFor(string line, bool isViolation)
    {
        // A guard whose only evidence is that it passes is a guard nobody has checked. This proves it can tell
        // the call it forbids from the ones it does not, on lines shaped like the ones in the codebase.
        var match = GetAllPattern().Match(line);

        Assert.True(match.Success);
        Assert.Equal(isViolation, _highVolumeStores.Contains(match.Groups["receiver"].Value, StringComparer.Ordinal));
    }

    private static string BuildMessage(IReadOnlyList<string> lines)
    {
        var message = new StringBuilder().AppendLine();

        foreach (var line in lines.Order(StringComparer.Ordinal))
        {
            message.Append("    ").AppendLine(line);
        }

        return message.ToString();
    }

    private static IEnumerable<(string RelativePath, int LineNumber, string Line)> EnumerateLines(string repositoryRoot)
    {
        foreach (var project in _scannedProjects)
        {
            var root = Path.Combine(repositoryRoot, "src", project);

            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                    file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                var relativePath = Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/');
                var lines = File.ReadAllLines(file);

                for (var i = 0; i < lines.Length; i++)
                {
                    yield return (relativePath, i + 1, lines[i]);
                }
            }
        }
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

    [GeneratedRegex(@"(?<receiver>[A-Za-z_][A-Za-z0-9_]*)\.GetAllAsync\(\s*\)")]
    private static partial Regex GetAllPattern();
}
