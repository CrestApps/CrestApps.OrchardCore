using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Bug: the activity editor and the telephony extension editor loaded every enabled user of the tenant into a
/// drop-down on each render, which times out on a tenant with many users. Editors pick a user with the searchable
/// user picker (<c>UserPicker</c> over the <c>CrestApps.Users.Search</c> endpoint) instead. This keeps an editor in
/// these modules from loading the whole user table again.
/// </summary>
public sealed partial class UserListLoadArchitectureTests
{
    private static readonly string[] _scannedFolders =
    [
        Path.Combine("src", "Modules", "CrestApps.OrchardCore.Omnichannel.Managements", "Drivers"),
        Path.Combine("src", "Modules", "CrestApps.OrchardCore.Omnichannel.Messaging", "Drivers"),
        Path.Combine("src", "Modules", "CrestApps.OrchardCore.ContactCenter", "Drivers"),
        Path.Combine("src", "Modules", "CrestApps.OrchardCore.Telephony", "Drivers"),
    ];

    [Fact]
    public void NoEditorDriver_LoadsEveryEnabledUser()
    {
        // Arrange
        var root = FindRepositoryRoot();
        var offenders = new List<string>();

        // Act
        foreach (var folder in _scannedFolders)
        {
            var path = Path.Combine(root, folder);

            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
            {
                if (AllEnabledUsersQuery().IsMatch(File.ReadAllText(file)))
                {
                    offenders.Add(Path.GetRelativePath(root, file));
                }
            }
        }

        // Assert
        Assert.True(offenders.Count == 0, "These drivers load every enabled user; use the UserPicker view component instead: " + string.Join(", ", offenders));
    }

    [GeneratedRegex(@"Query<User,\s*UserIndex>\(\s*\w+\s*=>\s*\w+\.IsEnabled\s*\)\s*\.ListAsync\(")]
    private static partial Regex AllEnabledUsersQuery();

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
