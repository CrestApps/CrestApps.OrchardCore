using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using OrchardCore.Settings;
using ReceiptsStartup = CrestApps.OrchardCore.Receipts.Startup;
using TelnyxSmsStartup = CrestApps.OrchardCore.Telnyx.SmsStartup;
using TransactionsNotificationStartup = CrestApps.OrchardCore.Transactions.NotificationStartup;
using UsersAvatarStartup = CrestApps.OrchardCore.Users.AvatarStartup;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Orchard Core's settings screen (<c>/Admin/Settings/{groupId}</c>) authorizes <c>ManageGroupSettings</c> with the
/// group id as the resource, and only the permissions registered for that group with
/// <c>AddSiteSettingsPermission(groupId, permission)</c> grant it. A settings driver that checks its own module
/// permission therefore also has to register that permission for its group, or a role that holds only the module
/// permission sees the menu item and then gets access denied.
/// </summary>
public sealed class SiteSettingsGroupPermissionRegistrationTests
{
    // The Orders and Customers settings groups are registered by their own change (pull request #958).
    private static readonly HashSet<string> _registeredElsewhere = new(StringComparer.Ordinal)
    {
        "OrderSettingsDisplayDriver",
        "CustomerCheckoutSettingsDisplayDriver",
        "CustomerPrivacySettingsDisplayDriver",
    };

    private static readonly Regex _driverDeclaration = new(@"class\s+(?<name>\w+)\s*:\s*SiteDisplayDriver<", RegexOptions.Compiled);
    private static readonly Regex _groupDeclaration = new(@"SettingsGroupId\s*=>\s*(?<group>[\w.]+)\s*;", RegexOptions.Compiled);
    private static readonly Regex _permissionCheck = new(@"AuthorizeAsync\(\s*[\w.?]+\s*,\s*(?<permission>[\w.]+)\s*\)", RegexOptions.Compiled);
    private static readonly Regex _registration = new(@"AddSiteSettingsPermission\(\s*(?<group>[\w.]+)\s*,\s*(?<permission>[\w.]+)\s*\)", RegexOptions.Compiled);
    private static readonly Regex _moduleStartupDeclaration = new(@"^public\s+(sealed\s+)?class\s+Startup\s*:\s*StartupBase", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex _classDeclaration = new(@"^(public|internal)\s+(sealed\s+)?class\s+\w+", RegexOptions.Compiled | RegexOptions.Multiline);

    [Fact]
    public void EverySettingsDriverWithItsOwnPermission_RegistersThatPermissionForItsGroup_InTheStartupThatRegistersTheDriver()
    {
        // Arrange
        var modulesRoot = Path.Combine(FindRepositoryRoot(), "src", "Modules");
        var offenders = new List<string>();
        var checkedDrivers = 0;

        // Act
        foreach (var moduleDirectory in Directory.EnumerateDirectories(modulesRoot))
        {
            var sources = EnumerateSources(moduleDirectory)
                .Select(path => (Path: path, Text: File.ReadAllText(path)))
                .ToArray();

            foreach (var (path, text) in sources)
            {
                var declaration = _driverDeclaration.Match(text);

                if (!declaration.Success)
                {
                    continue;
                }

                var driver = declaration.Groups["name"].Value;
                var groupMatch = _groupDeclaration.Match(text);
                var permissionMatch = _permissionCheck.Match(text);

                // A driver that checks no permission of its own relies on whatever already grants the group.
                if (!groupMatch.Success || !permissionMatch.Success || _registeredElsewhere.Contains(driver))
                {
                    continue;
                }

                var group = groupMatch.Groups["group"].Value;

                if (!group.Contains('.', StringComparison.Ordinal))
                {
                    group = $"{driver}.{group}";
                }

                var permission = permissionMatch.Groups["permission"].Value;
                var registeringStartups = FindRegisteringStartupBodies(sources, driver).ToArray();

                if (registeringStartups.Length == 0)
                {
                    offenders.Add($"{driver} ({Path.GetFileName(moduleDirectory)}): no startup registers the driver.");

                    continue;
                }

                checkedDrivers++;

                // The other features of a module depend on its base feature, so what the base startup registers for
                // the group applies to them too.
                var moduleStartup = FindModuleStartupBody(sources);

                foreach (var (startupPath, body) in registeringStartups)
                {
                    var registered = IsRegistered(body, group, permission) ||
                        (moduleStartup is not null && IsRegistered(moduleStartup, group, permission));

                    if (!registered)
                    {
                        offenders.Add($"{driver}: the startup in '{Path.GetFileName(startupPath)}' that registers it must call AddSiteSettingsPermission({group}, {permission}).");
                    }
                }
            }
        }

        // Assert
        Assert.True(checkedDrivers >= 30, $"Only {checkedDrivers} settings drivers were checked; the source scan no longer matches the code.");
        Assert.True(
            offenders.Count == 0,
            "These settings groups cannot be opened by a role that holds only the driver's permission:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void ReceiptsStartup_RegistersTheReceiptsGroup()
    {
        // Act
        var options = ConfigureServices(new ReceiptsStartup());

        // Assert
        AssertGranted(options, "receipts", "ManageReceiptSettings");
    }

    [Fact]
    public void TransactionRemindersStartup_RegistersTheTransactionsGroup()
    {
        // Act
        var options = ConfigureServices(new TransactionsNotificationStartup());

        // Assert
        AssertGranted(options, "transactions", "ManageTransactionSettings");
    }

    [Fact]
    public void UserAvatarsStartup_RegistersTheAvatarOptionsGroup()
    {
        // Act
        var options = ConfigureServices(new UsersAvatarStartup());

        // Assert
        AssertGranted(options, "avatarOptions", "ManageAvatarSettings");
    }

    [Fact]
    public void TelnyxSmsStartup_RegistersTheSmsGroupWithTheSmsSettingsPermission()
    {
        // Act
        var options = ConfigureServices(new TelnyxSmsStartup());

        // Assert
        AssertGranted(options, "sms", "ManageSmsSettings");
    }

    private static SiteSettingsPermissionOptions ConfigureServices(StartupBase startup)
    {
        var services = new ServiceCollection();

        startup.ConfigureServices(services);

        return services
            .BuildServiceProvider()
            .GetRequiredService<IOptions<SiteSettingsPermissionOptions>>()
            .Value;
    }

    private static void AssertGranted(SiteSettingsPermissionOptions options, string groupId, string permissionName)
    {
        Assert.True(options.GroupPermissions.TryGetValue(groupId, out var permissions), $"The '{groupId}' settings group is not registered.");
        Assert.Contains(permissions, permission => permission.Name == permissionName);
    }

    // The body of every startup class in the module that registers the driver, so the permission is registered by the
    // same feature that brings the settings screen, whatever features the tenant has enabled.
    private static IEnumerable<(string Path, string Body)> FindRegisteringStartupBodies((string Path, string Text)[] sources, string driver)
    {
        var registration = new Regex($@"(AddSiteDisplayDriver<{driver}>|IDisplayDriver<ISite>,\s*{driver}>)");

        foreach (var (path, text) in sources)
        {
            foreach (Match match in registration.Matches(text))
            {
                var classes = _classDeclaration.Matches(text);
                var start = classes.LastOrDefault(declaration => declaration.Index < match.Index)?.Index ?? 0;
                var end = classes.FirstOrDefault(declaration => declaration.Index > match.Index)?.Index ?? text.Length;

                yield return (path, text.Substring(start, end - start));
            }
        }
    }

    private static bool IsRegistered(string startupBody, string group, string permission)
        => _registration
            .Matches(startupBody)
            .Any(match => SameExpression(match.Groups["group"].Value, group) &&
                SameExpression(match.Groups["permission"].Value, permission));

    private static string FindModuleStartupBody((string Path, string Text)[] sources)
    {
        foreach (var (path, text) in sources)
        {
            if (!string.Equals(Path.GetFileName(path), "Startup.cs", StringComparison.Ordinal))
            {
                continue;
            }

            var declaration = _moduleStartupDeclaration.Match(text);

            if (!declaration.Success)
            {
                continue;
            }

            var next = _classDeclaration.Matches(text).FirstOrDefault(match => match.Index > declaration.Index);

            return text.Substring(declaration.Index, (next?.Index ?? text.Length) - declaration.Index);
        }

        return null;
    }

    // A constant can be named with or without its declaring type, so the shorter name only has to end the longer one.
    private static bool SameExpression(string left, string right)
        => string.Equals(left, right, StringComparison.Ordinal) ||
            left.EndsWith("." + right, StringComparison.Ordinal) ||
            right.EndsWith("." + left, StringComparison.Ordinal);

    private static IEnumerable<string> EnumerateSources(string root)
        => Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(file =>
                !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root (CrestApps.OrchardCore.slnx).");
    }
}
