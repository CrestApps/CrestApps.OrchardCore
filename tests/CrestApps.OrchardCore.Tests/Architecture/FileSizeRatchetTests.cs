using System.Text;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// A file past about eight hundred lines has stopped being one thing. Nobody reads it end to end, so a change is
/// made by finding the nearest similar-looking block and copying it, which is how the four duplicated HTTP
/// helpers and the three roll-up implementations in this codebase came to exist.
/// <para>
/// This is a ratchet, not a bar: the files that are already too long are listed with their current size, and the
/// test fails when a listed file grows or a new one crosses the line. Failing on the existing ten would mean
/// either ten refactors before any unrelated change could land, or the whole rule being suppressed — and a
/// suppressed rule stops anybody noticing the eleventh.
/// </para>
/// </summary>
public sealed class FileSizeRatchetTests
{
    /// <summary>
    /// The line count past which a file is expected to be split.
    /// </summary>
    private const int MaximumLines = 800;

    private static readonly string[] _scannedProjects =
    [
        Path.Combine("Core", "CrestApps.OrchardCore.ContactCenter.Core"),
        Path.Combine("Modules", "CrestApps.OrchardCore.ContactCenter"),
        Path.Combine("Modules", "CrestApps.OrchardCore.Telephony"),
        Path.Combine("Core", "CrestApps.OrchardCore.Telnyx.Core"),
        Path.Combine("Modules", "CrestApps.OrchardCore.Telnyx"),
        Path.Combine("Core", "CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core"),
        Path.Combine("Modules", "CrestApps.OrchardCore.Omnichannel.Sms.Portal"),
        Path.Combine("Core", "CrestApps.OrchardCore.Omnichannel.Voice.Core"),
        Path.Combine("Modules", "CrestApps.OrchardCore.Omnichannel.Voice"),
    ];

    /// <summary>
    /// The files that were already over the line when the ratchet was set, and the size each was at. A file may
    /// shrink freely — lowering its number here is the point — but it may not grow.
    /// </summary>
    private static readonly Dictionary<string, int> _existing = new(StringComparer.OrdinalIgnoreCase)
    {
        ["src/Modules/CrestApps.OrchardCore.Telephony/Hubs/TelephonyHub.cs"] = 1412,
        ["src/Modules/CrestApps.OrchardCore.ContactCenter/Reports/Providers/EnterpriseInteractionReportProvider.cs"] = 1317,
        ["src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/ContactCenterReportingService.cs"] = 1019,
        ["src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/ProviderVoiceEventService.cs"] = 974,
        ["src/Modules/CrestApps.OrchardCore.Omnichannel.Sms.Portal/Controllers/AdminController.cs"] = 955,
        ["src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/ActivityReservationService.cs"] = 888,
        ["src/Core/CrestApps.OrchardCore.Telnyx.Core/Services/TelnyxTelephonyProvider.cs"] = 812,
        ["src/Modules/CrestApps.OrchardCore.ContactCenter/Reports/Providers/AgentWorkforceReportProvider.cs"] = 833,
    };

    [Fact]
    public void NoNewFile_CrossesTheSizeLine_AndNoListedFileGrows()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var violations = new List<string>();

        // Act
        foreach (var (relativePath, lines) in EnumerateSourceFiles(repositoryRoot))
        {
            if (_existing.TryGetValue(relativePath, out var recorded))
            {
                if (lines > recorded)
                {
                    violations.Add($"{relativePath} grew from {recorded} to {lines} lines. Split it rather than adding to it.");
                }

                continue;
            }

            if (lines > MaximumLines)
            {
                violations.Add($"{relativePath} is {lines} lines, over the {MaximumLines}-line limit. Split it, or add it to the ratchet with a reason.");
            }
        }

        // Assert
        Assert.True(violations.Count == 0, BuildMessage(violations));
    }

    [Fact]
    public void EveryRatchetedFile_StillExists_AndIsStillOverTheLine()
    {
        // Arrange
        // A ratchet nobody prunes becomes a list of files that were long in 2026. When one is split or deleted,
        // its entry has to go, or the limit quietly stops applying to that path forever.
        var repositoryRoot = FindRepositoryRoot();
        var actual = EnumerateSourceFiles(repositoryRoot).ToDictionary(file => file.RelativePath, file => file.Lines, StringComparer.OrdinalIgnoreCase);
        var stale = new List<string>();

        // Act
        foreach (var (relativePath, recorded) in _existing)
        {
            if (!actual.TryGetValue(relativePath, out var lines))
            {
                stale.Add($"{relativePath} no longer exists; remove it from the ratchet.");
            }
            else if (lines <= MaximumLines)
            {
                stale.Add($"{relativePath} is now {lines} lines and no longer needs an exception; remove it from the ratchet.");
            }
            else if (lines < recorded)
            {
                stale.Add($"{relativePath} shrank from {recorded} to {lines} lines; lower its recorded size so the gain is locked in.");
            }
        }

        // Assert
        Assert.True(stale.Count == 0, BuildMessage(stale));
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

    private static IEnumerable<(string RelativePath, int Lines)> EnumerateSourceFiles(string repositoryRoot)
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

                yield return (
                    Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/'),
                    File.ReadAllLines(file).Length);
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
}
