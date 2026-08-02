using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Guards the rule that HTTP clients never auto-replay non-idempotent requests.
/// <para>
/// The standard resilience handler's retry strategy is method-agnostic by default and will replay a failed POST.
/// Telephony provider clients carry call-origination POSTs and OAuth authorization-code/refresh-token POSTs, so a
/// retry after a lost response could place a second outbound call or invalidate a one-time grant. The fix is a
/// single call to <c>options.Retry.DisableForUnsafeHttpMethods()</c>. Rather than trusting a hardcoded file list,
/// this guard discovers <em>every</em> source file under <c>src</c> that installs the standard resilience handler
/// and fails if any of them does not also disable unsafe-method retries, so a future provider cannot regress the
/// rule by adding a new client — the resulting double-dial would otherwise surface only in production.
/// </para>
/// </summary>
public sealed class ProviderHttpRetryArchitectureTests
{
    private static readonly Regex _standardHandlerRegex = new(
        @"AddStandardResilienceHandler",
        RegexOptions.Compiled);

    private static readonly Regex _disableUnsafeRetriesRegex = new(
        @"Retry\s*\.\s*DisableForUnsafeHttpMethods\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex _blockCommentRegex = new(
        @"/\*.*?\*/",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex _lineCommentRegex = new(
        @"//[^\r\n]*",
        RegexOptions.Compiled);

    [Fact]
    public void EverySourceFileThatAddsTheStandardResilienceHandler_DisablesRetriesForUnsafeHttpMethods()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var sourceRoot = Path.Combine(repositoryRoot, "src");
        var violations = new List<string>();
        var handlerInvocationCount = 0;

        // Act
        foreach (var fullPath in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (IsGeneratedOrBuildOutput(fullPath))
            {
                continue;
            }

            var source = StripComments(File.ReadAllText(fullPath));

            var handlerCount = _standardHandlerRegex.Matches(source).Count;

            if (handlerCount == 0)
            {
                continue;
            }

            handlerInvocationCount += handlerCount;

            // Require at least one disable call per handler invocation in the same file. Removing the exclusion
            // from any single handler drops the disable count below the handler count and fails the guard, so a
            // protected sibling handler in the same file can no longer mask an unprotected one.
            var disableCount = _disableUnsafeRetriesRegex.Matches(source).Count;

            if (disableCount < handlerCount)
            {
                var relativePath = Path.GetRelativePath(repositoryRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/');
                violations.Add($"{relativePath} ({handlerCount} handler(s), {disableCount} disable call(s))");
            }
        }

        // Assert
        Assert.True(
            handlerInvocationCount > 0,
            "Expected at least one source file to install the standard resilience handler, but none were found. " +
            "The guard may be scanning the wrong location and would pass vacuously.");

        Assert.True(
            violations.Count == 0,
            "These source files add the standard resilience handler without disabling retries for unsafe HTTP " +
            "methods on every handler, so a failed call-origination or OAuth POST could be replayed: " +
            $"{string.Join(", ", violations)}.");
    }

    private static string StripComments(string source)
    {
        var withoutBlockComments = _blockCommentRegex.Replace(source, string.Empty);

        return _lineCommentRegex.Replace(withoutBlockComments, string.Empty);
    }

    private static bool IsGeneratedOrBuildOutput(string fullPath)
    {
        var normalized = fullPath.Replace(Path.DirectorySeparatorChar, '/');

        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase);
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
