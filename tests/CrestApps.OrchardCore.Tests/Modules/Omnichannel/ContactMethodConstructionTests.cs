using System.Text;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

/// <summary>
/// How a contact method is allowed to be created.
/// </summary>
/// <remarks>
/// OrchardCore keys a bag's items by content item id when it applies an edit. An item with no id makes
/// <c>BagPartDisplayDriver</c> throw, the display coordinator swallows the exception, and the entire bag update is
/// discarded -- so an agent edits a phone number, publishes, and the page returns with the old number and no error
/// anywhere on screen.
/// <para>
/// Creating the item through the content manager is what gives it an id, along with its type's parts and defaults.
/// Two separate writers have now built one with a bare <c>new ContentItem</c> instead: the importer, which was
/// fixed by assigning an id by hand, and the automated conversation's email capture, which was not, and which
/// silently broke saving on every contact it touched. A source rule is used here rather than a behavioural test
/// because the damage happens inside OrchardCore's own driver, and because what needs catching is the next writer
/// to be added -- not the two that are already correct.
/// </para>
/// </remarks>
public sealed class ContactMethodConstructionTests
{
    [Fact]
    public void EveryWriterOfAContactMethod_GivesItAnIdentifier()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var sourceRoot = Path.Combine(repositoryRoot, "src");
        var violations = new List<string>();

        // Act
        foreach (var path in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var source = File.ReadAllText(path);

            // Only files that actually put something into the contact-methods bag are in scope.
            if (!source.Contains("ContactMethods", StringComparison.Ordinal) ||
                !source.Contains("new ContentItem", StringComparison.Ordinal))
            {
                continue;
            }

            // Either route is safe: the content manager assigns the id, or the writer assigns one itself.
            var assignsIdentifier = source.Contains("ContentItemId =", StringComparison.Ordinal) ||
                source.Contains("NewAsync(", StringComparison.Ordinal);

            if (!assignsIdentifier)
            {
                violations.Add(Path.GetRelativePath(repositoryRoot, path));
            }
        }

        // Assert
        Assert.True(violations.Count == 0, BuildMessage(violations));
    }

    private static string BuildMessage(List<string> violations)
    {
        var message = new StringBuilder();
        message.AppendLine("A contact method is created here without an identifier, which stops the whole bag from saving:");
        message.AppendLine();

        foreach (var violation in violations)
        {
            message.Append("    ").AppendLine(violation);
        }

        message.AppendLine();
        message.AppendLine("Create it with IContentManager.NewAsync(\"<type>\") so it is built like any other content item.");

        return message.ToString();
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
}
