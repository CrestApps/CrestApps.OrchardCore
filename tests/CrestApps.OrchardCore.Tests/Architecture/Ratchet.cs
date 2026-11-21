using System.Collections.Concurrent;
using System.Text;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// The pieces every shrink-only ratchet shares: the scan of the source tree, and the two comparisons against the
/// allowlist that record today's offenders.
/// </summary>
/// <remarks>
/// A ratchet holds each offender with a measure: a number of copies, of lines or of parameters. The first comparison
/// fails when an offender is new or its measure grew. The second fails when a listed offender is gone or its measure
/// dropped, so a fix is locked in by lowering the entry instead of leaving room for the problem to come back.
/// </remarks>
internal static class Ratchet
{
    private static readonly Lazy<string> _repositoryRoot = new(ProjectReferenceRulesTests.FindRepositoryRoot);

    private static readonly ConcurrentDictionary<string, IReadOnlyList<SourceFile>> _listings = new(StringComparer.Ordinal);

    private static readonly ConcurrentDictionary<string, string> _texts = new(StringComparer.Ordinal);

    public static string RepositoryRoot => _repositoryRoot.Value;

    /// <summary>
    /// Lists the files under a folder of the repository, skipping build output, packages and generated assets.
    /// </summary>
    /// <param name="folder">The folder, relative to the repository root, such as <c>src</c>.</param>
    /// <param name="searchPattern">The file pattern, such as <c>*.cs</c>.</param>
    /// <remarks>
    /// The listing and the text of each file are read once per test run and shared by every ratchet, which keeps a
    /// dozen scans of ten thousand files from each walking and reading the tree again.
    /// </remarks>
    public static IReadOnlyList<SourceFile> EnumerateFiles(string folder, string searchPattern = "*.cs")
        => _listings.GetOrAdd($"{folder}|{searchPattern}", _ =>
        {
            var root = Path.Combine(RepositoryRoot, folder);

            return SourceTreeFiles.Enumerate(root, searchPattern, IsSkippedDirectory)
                .Select(file => new SourceFile(file, Path.GetRelativePath(RepositoryRoot, file).Replace('\\', '/')))
                .ToList();
        });

    /// <summary>
    /// Reads a file listed by <see cref="EnumerateFiles"/>, once per test run.
    /// </summary>
    public static string ReadAllText(SourceFile file)
        => _texts.GetOrAdd(file.FullPath, File.ReadAllText);

    /// <summary>
    /// Fails when an offender is not on the allowlist, or when its measure is above the one recorded.
    /// </summary>
    /// <param name="actual">Today's offenders and their measures.</param>
    /// <param name="allowed">The allowlist.</param>
    /// <param name="problem">What the offenders do, starting a sentence: "These files ...".</param>
    /// <param name="instead">What to do instead.</param>
    public static void AssertNothingAdded(IReadOnlyDictionary<string, int> actual, IReadOnlyDictionary<string, int> allowed, string problem, string instead)
    {
        var added = new List<string>();

        foreach (var (key, measure) in actual)
        {
            var recorded = allowed.GetValueOrDefault(key);

            if (measure > recorded)
            {
                added.Add($"{key}: {measure} (allowed {recorded})");
            }
        }

        Assert.True(added.Count == 0, BuildMessage($"{problem} {instead} The allowlist only shrinks: never add an entry or raise a number.", added));
    }

    /// <summary>
    /// Fails when an allowlisted offender no longer offends, or offends less than recorded.
    /// </summary>
    /// <param name="actual">Today's offenders and their measures.</param>
    /// <param name="allowed">The allowlist.</param>
    /// <param name="allowlistName">Where the allowlist is, such as <c>MethodLengthRatchetTests._known</c>.</param>
    public static void AssertNothingStale(IReadOnlyDictionary<string, int> actual, IReadOnlyDictionary<string, int> allowed, string allowlistName)
    {
        var stale = new List<string>();

        foreach (var (key, recorded) in allowed)
        {
            var measure = actual.GetValueOrDefault(key);

            if (measure == 0)
            {
                stale.Add($"{key} no longer offends. Delete its entry.");
            }
            else if (measure < recorded)
            {
                stale.Add($"{key} is at {measure}, below the {recorded} recorded. Lower its entry to {measure}.");
            }
        }

        Assert.True(stale.Count == 0, BuildMessage($"Offenders were fixed. Update {allowlistName} so the problem cannot come back:", stale));
    }

    public static string BuildMessage(string header, IEnumerable<string> lines)
    {
        var message = new StringBuilder().AppendLine(header);

        foreach (var line in lines.Order(StringComparer.Ordinal))
        {
            message.Append("    ").AppendLine(line);
        }

        return message.ToString();
    }

    /// <summary>
    /// Counts the line a position in a text is on, starting at one.
    /// </summary>
    public static int LineAt(string text, int index)
        => text.AsSpan(0, index).Count('\n') + 1;

    private static bool IsSkippedDirectory(string directory)
        => Path.GetFileName(directory) is "bin" or "obj" or "node_modules" or "wwwroot";

    public sealed record SourceFile(string FullPath, string RelativePath);
}
