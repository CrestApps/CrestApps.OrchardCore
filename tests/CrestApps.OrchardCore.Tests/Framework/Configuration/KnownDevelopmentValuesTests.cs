using System.Diagnostics;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Configuration;

namespace CrestApps.OrchardCore.Tests.Framework.Configuration;

/// <summary>
/// Covers the register of values that must never authenticate anything in production, and keeps that register
/// honest against the development assets it describes.
/// </summary>
public sealed partial class KnownDevelopmentValuesTests
{
    /// <summary>
    /// File extensions whose contents are scanned for credential assignments. These are the formats the
    /// repository uses to configure the development telephony and media stack.
    /// </summary>
    private static readonly string[] _scannedExtensions =
    [
        ".conf",
        ".env",
        ".ini",
        ".template",
    ];

    [Theory]
    [InlineData("changeme")]
    [InlineData("ChangeMe")]
    [InlineData("CHANGEME")]
    [InlineData("password")]
    [InlineData("secret")]
    [InlineData("admin")]
    [InlineData("placeholder")]
    [InlineData("  changeme  ")]
    public void IsDevelopmentValue_ForAPlaceholderWord_ReportsTheValueAsDevelopmentOnly(string value)
    {
        Assert.True(KnownDevelopmentValues.IsDevelopmentValue(value, out var reason));
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    [Theory]
    [InlineData("<replace-with-a-real-secret>")]
    [InlineData("[replace-with-a-real-secret]")]
    [InlineData("{{turn_secret}}")]
    [InlineData("__TURN_SECRET__")]
    [InlineData("replace-with-your-own-value")]
    [InlineData("TODO: generate a secret")]
    public void IsDevelopmentValue_ForAnUnsubstitutedTemplatePlaceholder_ReportsTheValueAsDevelopmentOnly(string value)
    {
        Assert.True(KnownDevelopmentValues.IsDevelopmentValue(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsDevelopmentValue_ForAnAbsentValue_ReportsNothing(string value)
    {
        // An absent secret is a different failure with a different remedy. Reporting it here would tell an
        // operator to replace a development credential they never configured.
        Assert.False(KnownDevelopmentValues.IsDevelopmentValue(value, out var reason));
        Assert.Null(reason);
    }

    [Theory]
    [InlineData("P@ssw0rd-that-is-genuinely-random-6f2c1a")]
    [InlineData("testing-the-limits-of-a-real-production-secret-9f31")]
    [InlineData("Sample8fJk21mQ")]
    [InlineData("a-production-secret-for-the-development-team")]
    public void IsDevelopmentValue_ForARealSecretThatMerelyResemblesATestValue_ReportsNothing(string value)
    {
        // Recognition is by exact match against a closed register rather than by heuristic scoring, so an
        // operator whose genuine secret contains a word like "test" or "sample" is never locked out.
        Assert.False(KnownDevelopmentValues.IsDevelopmentValue(value));
    }

    [Fact]
    public void IsDevelopmentValue_ForACredentialPublishedInThisRepository_ReportsTheValueAsDevelopmentOnly()
    {
        var assignments = ReadTrackedCredentialAssignments();

        Assert.NotEmpty(assignments);

        foreach (var assignment in assignments)
        {
            Assert.True(
                KnownDevelopmentValues.IsDevelopmentValue(assignment.Value),
                $"The credential assigned by '{assignment.Key}' in '{assignment.RelativePath}' line {assignment.LineNumber} " +
                "is checked into this repository but is not recognized as a development value, so nothing stops it " +
                "from being used in production. Register its SHA-256 digest in KnownDevelopmentValues.");
        }
    }

    [Fact]
    public void CheckedInSecretDigests_AreAllStillPresentInTheTrackedDevelopmentAssets()
    {
        // A digest that no longer matches any asset is dead weight that makes the register look better
        // maintained than it is, and hides the fact that the asset it described was renamed or deleted.
        var observedDigests = ReadTrackedCredentialAssignments()
            .Select(assignment => KnownDevelopmentValues.ComputeDigest(assignment.Value.Trim()))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var registeredDigests = KnownDevelopmentValues.GetCheckedInSecretDigests();

        Assert.NotEmpty(registeredDigests);

        var stale = registeredDigests
            .Where(digest => !observedDigests.Contains(digest))
            .ToArray();

        Assert.True(
            stale.Length == 0,
            "These digests are registered as checked-in development credentials but no longer appear in any " +
            $"tracked development asset: {string.Join(", ", stale)}.");
    }

    [Fact]
    public void ComputeDigest_IsStableAndCaseSensitive()
    {
        Assert.Equal(KnownDevelopmentValues.ComputeDigest("value"), KnownDevelopmentValues.ComputeDigest("value"));
        Assert.NotEqual(KnownDevelopmentValues.ComputeDigest("value"), KnownDevelopmentValues.ComputeDigest("Value"));
        Assert.Equal(64, KnownDevelopmentValues.ComputeDigest("value").Length);
    }

    /// <summary>
    /// Reads every credential assignment from the repository's tracked development assets.
    /// </summary>
    /// <remarks>
    /// Only tracked files are scanned. Untracked working state such as <c>App_Data</c> holds real credentials
    /// for whoever is running the stack locally, and those are not published, so demanding they be registered
    /// would be both wrong and impossible to satisfy.
    /// </remarks>
    private static List<CredentialAssignment> ReadTrackedCredentialAssignments()
    {
        var repositoryRoot = FindRepositoryRoot();
        var assignments = new List<CredentialAssignment>();

        foreach (var relativePath in ListTrackedFiles(repositoryRoot))
        {
            if (!_scannedExtensions.Any(extension => relativePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var fullPath = Path.Combine(repositoryRoot, relativePath);

            if (!File.Exists(fullPath))
            {
                continue;
            }

            var lines = File.ReadAllLines(fullPath);

            for (var index = 0; index < lines.Length; index++)
            {
                var match = CredentialAssignmentPattern().Match(lines[index]);

                if (!match.Success)
                {
                    continue;
                }

                var value = match.Groups["value"].Value.Trim();

                // A value that defers to the environment is not a credential; it is the absence of one.
                if (value.Length == 0 || value.Contains('$', StringComparison.Ordinal))
                {
                    continue;
                }

                assignments.Add(new CredentialAssignment(
                    relativePath,
                    index + 1,
                    match.Groups["key"].Value,
                    value));
            }
        }

        return assignments;
    }

    private static string[] ListTrackedFiles(string repositoryRoot)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "ls-files",
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        });

        Assert.NotNull(process);

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        Assert.Equal(0, process.ExitCode);

        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return directory.FullName;
    }

    [GeneratedRegex(
        @"^\s*(?<key>[A-Za-z0-9_.\-]*(?:secret|password|passwd|apikey|api_key|token))\s*[=:]\s*""?(?<value>[^""\r\n]+?)""?\s*$",
        RegexOptions.IgnoreCase)]
    private static partial Regex CredentialAssignmentPattern();

    private sealed record CredentialAssignment(
        string RelativePath,
        int LineNumber,
        string Key,
        string Value);
}
