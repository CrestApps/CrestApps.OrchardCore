using System.Text;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// UX-4: a view never shows a stored UTC value as it is. A time goes through the <c>DateTime</c> shape or
/// <c>ILocalClock</c>; a machine format (a <c>datetime</c>, <c>value</c> or <c>data-</c> attribute, or an invariant
/// format) is fine. Pure dates (a due date picked as a day) are the AGENTS.md exception and are listed here.
/// </summary>
public sealed partial class ViewUtcFormattingArchitectureTests
{
    // Pure dates: picked in a date input and stored as that day, so the date part is the value.
    private static readonly Dictionary<string, int> _pureDates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["src/Modules/CrestApps.OrchardCore.Subscriptions/Views/InstallmentPlans/Detail.cshtml"] = 3,
        ["src/Modules/CrestApps.OrchardCore.Subscriptions/Views/InstallmentPlans/Pay.cshtml"] = 1,
        ["src/Modules/CrestApps.OrchardCore.Subscriptions/Views/Items/InstallmentPlan.NextPayment.SummaryAdmin.cshtml"] = 1,
    };

    [Fact]
    public void NoViewFormatsAStoredUtcValueForPeople()
    {
        var root = FindRepositoryRoot();
        var problems = new List<string>();
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in SourceTreeFiles.Enumerate(Path.Combine(root, "src"), "*.cshtml", directory => SourceTreeFiles.IsBuildOutputDirectory(directory) || Path.GetFileName(directory) is "node_modules"))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            var lines = File.ReadAllLines(file);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (UtcToString().IsMatch(line) && !IsMachineFormat(line))
                {
                    problems.Add($"{relative}({i + 1}): a UTC value is formatted with ToString. Use the DateTime shape or ILocalClock.");
                }

                var inLocalizer = UtcInLocalizer().Count(line);

                if (inLocalizer > 0)
                {
                    counts[relative] = counts.GetValueOrDefault(relative) + inLocalizer;
                }
            }
        }

        foreach (var (file, count) in counts)
        {
            if (count > _pureDates.GetValueOrDefault(file))
            {
                problems.Add($"{file}: a UTC value is passed to T[...] {count} time(s). Convert it with ILocalClock first, or record a pure date here.");
            }
        }

        foreach (var (file, known) in _pureDates)
        {
            if (counts.GetValueOrDefault(file) < known)
            {
                problems.Add($"{file} now passes fewer UTC values to T[...]; lower its entry.");
            }
        }

        Assert.True(problems.Count == 0, new StringBuilder().AppendLine().AppendJoin(Environment.NewLine, problems).ToString());
    }

    private static bool IsMachineFormat(string line)
        => line.Contains("datetime=\"", StringComparison.Ordinal) ||
            line.Contains("value=\"", StringComparison.Ordinal) ||
            line.Contains("data-", StringComparison.Ordinal) ||
            line.Contains("InvariantCulture", StringComparison.Ordinal);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests", "CrestApps.OrchardCore.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The repository root could not be found.");
    }

    [GeneratedRegex(@"Utc\??(\.Value)?\.ToString\(", RegexOptions.CultureInvariant)]
    private static partial Regex UtcToString();

    [GeneratedRegex(@"T\[[^\]]*,\s*[\w.?]*Utc\]", RegexOptions.CultureInvariant)]
    private static partial Regex UtcInLocalizer();
}
