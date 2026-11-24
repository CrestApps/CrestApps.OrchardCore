using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Learning tells site users through the Notifications module, never by e-mail it sends itself, so each user chooses
/// how each kind of notice reaches them; and a notice links to its record with a site-relative <c>~/</c> URL, never one
/// built from the tenant's address.
/// </summary>
public sealed partial class LearningNotificationArchitectureTests
{
    [Fact]
    public void NoLearningCode_EmailsASiteUserDirectly()
    {
        var offenders = LearningSources()
            .Where(file => EmailSending().IsMatch(File.ReadAllText(file)))
            .Select(Relative)
            .ToArray();

        Assert.True(offenders.Length == 0, "Send Learning notices with INotificationService, not e-mail: " + string.Join(", ", offenders));
    }

    [Fact]
    public void LearningNotices_LinkWithASiteRelativeUrl_NotTheTenantPrefix()
    {
        var offenders = LearningSources()
            .Where(file => file.EndsWith(".cs", StringComparison.Ordinal))
            .Where(file =>
            {
                var text = File.ReadAllText(file);

                return text.Contains("LearningNotificationMessage", StringComparison.Ordinal) && text.Contains("RequestUrlPrefix", StringComparison.Ordinal);
            })
            .Select(Relative)
            .ToArray();

        Assert.True(offenders.Length == 0, "A notification URL starts with ~/, which Notifications resolves for the tenant: " + string.Join(", ", offenders));
    }

    private static IEnumerable<string> LearningSources()
    {
        var src = Path.Combine(RepositoryRoot, "src");

        return new[] { "Abstractions", "Core", "Modules" }
            .Select(folder => Path.Combine(src, folder))
            .SelectMany(folder => Directory.EnumerateDirectories(folder, "CrestApps.OrchardCore.Learning*"))
            .SelectMany(project => Directory.EnumerateFiles(project, "*.*", SearchOption.AllDirectories))
            .Where(file => file.EndsWith(".cs", StringComparison.Ordinal) || file.EndsWith(".cshtml", StringComparison.Ordinal))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }

    private static string Relative(string file)
        => Path.GetRelativePath(RepositoryRoot, file);

    private static string RepositoryRoot { get; } = FindRepositoryRoot();

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

    [GeneratedRegex(@"\b(IEmailService|ISmtpService|IEmailProvider|MailMessage|SendEmailAsync)\b")]
    private static partial Regex EmailSending();
}
