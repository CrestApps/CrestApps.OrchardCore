using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc.Localization;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// SEC-FE4 h: a <see cref="LocalizedHtmlString"/> built with a message (a provider error, a step result) as its value
/// uses that message as a composite format string when it is written, so a brace in the message throws while the notice
/// renders. A message is an argument: <c>new LocalizedHtmlString(name, "{0}", false, message)</c>, which also encodes it.
/// </summary>
public sealed class LocalizedHtmlStringMessageFormatArchitectureTests
{
    private const string Construction = "new LocalizedHtmlString(";

    [Fact]
    public void AMessageWithABrace_RendersWhenItIsAnArgument()
    {
        var message = new LocalizedHtmlString("error", "{0}", false, "Card <declined> {code: 42}");
        using var writer = new StringWriter();

        message.WriteTo(writer, HtmlEncoder.Default);

        Assert.Equal("Card &lt;declined&gt; {code: 42}", writer.ToString());
    }

    [Fact]
    public void AMessageWithABrace_ThrowsWhenItIsTheFormat()
    {
        var message = new LocalizedHtmlString("error", HtmlEncoder.Default.Encode("Card {code: 42}"));
        using var writer = new StringWriter();

        Assert.Throws<FormatException>(() => message.WriteTo(writer, HtmlEncoder.Default));
    }

    [Fact]
    public void Sources_NeverUseAnEncodedMessageAsTheFormat()
    {
        var repositoryRoot = FindRepositoryRoot();
        var violations = new List<string>();

        foreach (var fullPath in Directory.EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.cs", SearchOption.AllDirectories))
        {
            var normalized = fullPath.Replace(Path.DirectorySeparatorChar, '/');

            if (normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase) || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = File.ReadAllText(fullPath);
            var index = text.IndexOf(Construction, StringComparison.Ordinal);

            while (index >= 0)
            {
                var end = text.IndexOf(';', index);
                var statement = end < 0 ? text[index..] : text[index..end];

                if (statement.Contains("HtmlEncoder.Default.Encode(", StringComparison.Ordinal))
                {
                    var line = text.AsSpan(0, index).Count('\n') + 1;
                    violations.Add($"{Path.GetRelativePath(repositoryRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/')}:{line}");
                }

                index = text.IndexOf(Construction, index + 1, StringComparison.Ordinal);
            }
        }

        Assert.True(
            violations.Count == 0,
            "These build a LocalizedHtmlString whose format is an encoded message, which throws on a brace. Pass the message " +
            $"as an argument instead: new LocalizedHtmlString(name, \"{{0}}\", false, message): {string.Join(", ", violations)}.");
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

        throw new InvalidOperationException("The repository root was not found.");
    }
}
