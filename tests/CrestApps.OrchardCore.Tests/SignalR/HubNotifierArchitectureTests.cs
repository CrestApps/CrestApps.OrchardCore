using System.Text;

namespace CrestApps.OrchardCore.Tests.SignalR;

/// <summary>
/// A real-time notifier must address a tenant-qualified group. Orchard maps a hub per tenant but the hub type and
/// the SignalR backplane are shared, so <c>Clients.All</c> crosses tenants and reaches users without the
/// permission that the notification's data belongs to.
/// </summary>
public sealed class HubNotifierArchitectureTests
{
    [Fact]
    public void NoNotifier_BroadcastsToClientsAll()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var notifiers = EnumerateNotifierSources(Path.Combine(repositoryRoot, "src")).ToArray();

        Assert.NotEmpty(notifiers);

        // Act
        var offenders = notifiers
            .Where(file => File.ReadAllText(file).Contains("Clients.All", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(repositoryRoot, file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Assert
        Assert.True(offenders.Length == 0, BuildMessage(offenders));
    }

    private static IEnumerable<string> EnumerateNotifierSources(string root)
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(root, "*Notifier.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => File.ReadAllText(file).Contains("IHubContext", StringComparison.Ordinal));
    }

    private static string BuildMessage(IEnumerable<string> offenders)
    {
        var builder = new StringBuilder("A hub notifier must target a tenant-qualified group, never Clients.All:");

        builder.AppendLine();

        foreach (var offender in offenders)
        {
            builder.Append("  - ").AppendLine(offender);
        }

        return builder.ToString();
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
