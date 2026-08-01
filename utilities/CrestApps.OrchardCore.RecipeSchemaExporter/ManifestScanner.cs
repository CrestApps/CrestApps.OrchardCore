using System.Text.RegularExpressions;

#nullable enable

namespace CrestApps.OrchardCore.RecipeSchemaExporter;

/// <summary>
/// Scans Manifest.cs and constants files in OrchardCore and CrestApps.OrchardCore
/// source trees to discover all feature and theme IDs.
/// </summary>
internal static partial class ManifestScanner
{
    private const string _orchardCoreSourceEnvVar = "ORCHARDCORE_SOURCE_DIR";

    /// <summary>
    /// Discovers all feature IDs from both CrestApps.OrchardCore and OrchardCore source trees.
    /// </summary>
    /// <param name="repositoryRoot">The CrestApps.OrchardCore repository root.</param>
    /// <returns>A sorted, deduplicated array of all discovered feature IDs.</returns>
    public static string[] DiscoverFeatureIds(string repositoryRoot)
    {
        var featureIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Scan CrestApps.OrchardCore modules.
        var crestAppsModulesPath = Path.Combine(repositoryRoot, "src", "Modules");
        if (Directory.Exists(crestAppsModulesPath))
        {
            ScanModulesDirectory(crestAppsModulesPath, featureIds);
        }

        // Scan CrestApps.OrchardCore source for feature constants.
        var crestAppsSrcPath = Path.Combine(repositoryRoot, "src");
        if (Directory.Exists(crestAppsSrcPath))
        {
            ScanConstantsFiles(crestAppsSrcPath, featureIds, "CrestApps");
        }

        // Scan OrchardCore source if available.
        var orchardCoreSrcRoot = FindOrchardCoreSource(repositoryRoot);
        if (orchardCoreSrcRoot != null)
        {
            var orchardModulesPath = Path.Combine(orchardCoreSrcRoot, "src", "OrchardCore.Modules");
            if (Directory.Exists(orchardModulesPath))
            {
                ScanModulesDirectory(orchardModulesPath, featureIds);
            }

            var orchardThemesPath = Path.Combine(orchardCoreSrcRoot, "src", "OrchardCore.Themes");
            if (Directory.Exists(orchardThemesPath))
            {
                ScanModulesDirectory(orchardThemesPath, featureIds);
            }

            var orchardCoreSrcPath = Path.Combine(orchardCoreSrcRoot, "src");
            if (Directory.Exists(orchardCoreSrcPath))
            {
                ScanConstantsFiles(orchardCoreSrcPath, featureIds, "OrchardCore");
            }
        }
        else
        {
            // Fall back to a comprehensive list of known OrchardCore features.
            foreach (var featureId in GetKnownOrchardCoreFeatureIds())
            {
                featureIds.Add(featureId);
            }
        }

        return featureIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Discovers all theme IDs from both CrestApps.OrchardCore and OrchardCore source trees.
    /// </summary>
    /// <param name="repositoryRoot">The CrestApps.OrchardCore repository root.</param>
    /// <returns>A sorted, deduplicated array of all discovered theme IDs.</returns>
    public static string[] DiscoverThemeIds(string repositoryRoot)
    {
        var themeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var orchardCoreSrcRoot = FindOrchardCoreSource(repositoryRoot);
        if (orchardCoreSrcRoot != null)
        {
            var themesPath = Path.Combine(orchardCoreSrcRoot, "src", "OrchardCore.Themes");
            if (Directory.Exists(themesPath))
            {
                foreach (var directory in Directory.GetDirectories(themesPath))
                {
                    themeIds.Add(Path.GetFileName(directory));
                }
            }
        }
        else
        {
            foreach (var themeId in GetKnownOrchardCoreThemeIds())
            {
                themeIds.Add(themeId);
            }
        }

        return themeIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Discovers all workflow activity type names from both CrestApps.OrchardCore and OrchardCore source trees,
    /// classified into events and tasks.
    /// </summary>
    /// <param name="repositoryRoot">The CrestApps.OrchardCore repository root.</param>
    /// <returns>A tuple containing the sorted, deduplicated event and task activity type names.</returns>
    public static (string[] Events, string[] Tasks) DiscoverWorkflowActivities(string repositoryRoot)
    {
        var events = new HashSet<string>(StringComparer.Ordinal);
        var tasks = new HashSet<string>(StringComparer.Ordinal);

        // Scan CrestApps.OrchardCore source for workflow activities.
        var crestAppsSrcPath = Path.Combine(repositoryRoot, "src");
        if (Directory.Exists(crestAppsSrcPath))
        {
            ScanActivityClasses(crestAppsSrcPath, events, tasks);
        }

        // Scan OrchardCore source if available, otherwise fall back to the known built-in activities.
        var orchardCoreSrcRoot = FindOrchardCoreSource(repositoryRoot);
        if (orchardCoreSrcRoot != null)
        {
            var orchardSrcPath = Path.Combine(orchardCoreSrcRoot, "src");
            if (Directory.Exists(orchardSrcPath))
            {
                ScanActivityClasses(orchardSrcPath, events, tasks);
            }
        }
        else
        {
            foreach (var eventName in WorkflowActivityCatalog.KnownEventActivities)
            {
                events.Add(eventName);
            }

            foreach (var taskName in WorkflowActivityCatalog.KnownTaskActivities)
            {
                tasks.Add(taskName);
            }
        }

        return (Sort(events), Sort(tasks));
    }

    /// <summary>
    /// Discovers all permission names from both CrestApps.OrchardCore and OrchardCore source trees.
    /// </summary>
    /// <param name="repositoryRoot">The CrestApps.OrchardCore repository root.</param>
    /// <returns>A sorted, deduplicated array of all discovered permission names.</returns>
    public static string[] DiscoverPermissionNames(string repositoryRoot)
    {
        var permissionNames = new HashSet<string>(StringComparer.Ordinal);

        // Scan CrestApps.OrchardCore source for permission declarations.
        var crestAppsSrcPath = Path.Combine(repositoryRoot, "src");
        if (Directory.Exists(crestAppsSrcPath))
        {
            ScanPermissionNames(crestAppsSrcPath, permissionNames);
        }

        // Scan OrchardCore source if available, otherwise fall back to the known built-in permissions.
        var orchardCoreSrcRoot = FindOrchardCoreSource(repositoryRoot);
        if (orchardCoreSrcRoot != null)
        {
            var orchardSrcPath = Path.Combine(orchardCoreSrcRoot, "src");
            if (Directory.Exists(orchardSrcPath))
            {
                ScanPermissionNames(orchardSrcPath, permissionNames);
            }
        }
        else
        {
            foreach (var permissionName in PermissionCatalog.KnownPermissions)
            {
                permissionNames.Add(permissionName);
            }
        }

        return Sort(permissionNames);
    }

    private static void ScanActivityClasses(
        string srcPath,
        HashSet<string> events,
        HashSet<string> tasks)
    {
        var activityFiles = Directory.EnumerateFiles(srcPath, "*.cs", SearchOption.AllDirectories)
            .Where(file => file.Replace('\\', '/').Contains("/Workflows/", StringComparison.OrdinalIgnoreCase));

        foreach (var filePath in activityFiles)
        {
            var content = File.ReadAllText(filePath);

            foreach (Match match in ActivityClassRegex().Matches(content))
            {
                var modifiers = match.Groups["modifiers"].Value;
                var name = match.Groups["name"].Value;
                var bases = match.Groups["bases"].Value;

                if (modifiers.Contains("abstract", StringComparison.Ordinal) ||
                    bases.Contains("IBackgroundTask", StringComparison.Ordinal))
                {
                    continue;
                }

                if (name.EndsWith("Event", StringComparison.Ordinal))
                {
                    events.Add(name);
                }
                else if (name.EndsWith("Task", StringComparison.Ordinal) &&
                    !name.EndsWith("BackgroundTask", StringComparison.Ordinal))
                {
                    tasks.Add(name);
                }
            }
        }
    }

    private static void ScanPermissionNames(string srcPath, HashSet<string> permissionNames)
    {
        var permissionFiles = Directory.EnumerateFiles(srcPath, "*.cs", SearchOption.AllDirectories)
            .Where(file => Path.GetFileName(file).Contains("Permission", StringComparison.OrdinalIgnoreCase));

        foreach (var filePath in permissionFiles)
        {
            var content = File.ReadAllText(filePath);

            foreach (Match match in PermissionNameRegex().Matches(content))
            {
                var name = match.Groups[1].Value;

                // Skip dynamic, template-based permission names such as "Edit_{0}".
                if (!string.IsNullOrWhiteSpace(name) &&
                    !name.Contains(' ') &&
                    !name.Contains('{') &&
                    name.Length < 64)
                {
                    permissionNames.Add(name);
                }
            }
        }
    }

    private static string[] Sort(HashSet<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void ScanModulesDirectory(string modulesPath, HashSet<string> featureIds)
    {
        foreach (var moduleDirectory in Directory.GetDirectories(modulesPath))
        {
            var folderName = Path.GetFileName(moduleDirectory);

            // The folder name is always a valid default feature ID for the module.
            featureIds.Add(folderName);

            // Parse Manifest.cs for explicit feature IDs.
            var manifestPath = Path.Combine(moduleDirectory, "Manifest.cs");
            if (File.Exists(manifestPath))
            {
                ExtractStringLiteralIds(manifestPath, featureIds);
            }
        }
    }

    private static void ScanConstantsFiles(string srcPath, HashSet<string> featureIds, string prefix)
    {
        var constantsFiles = Directory.EnumerateFiles(srcPath, "*.cs", SearchOption.AllDirectories)
            .Where(file =>
            {
                var fileName = Path.GetFileName(file);

                return fileName.Contains("Constants", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Contains("Permissions", StringComparison.OrdinalIgnoreCase);
            });

        foreach (var filePath in constantsFiles)
        {
            ExtractFeatureConstants(filePath, featureIds, prefix);
        }
    }

    private static void ExtractStringLiteralIds(string manifestPath, HashSet<string> featureIds)
    {
        var content = File.ReadAllText(manifestPath);
        var matches = IdAssignmentRegex().Matches(content);

        foreach (Match match in matches)
        {
            var id = match.Groups[1].Value;

            if (!string.IsNullOrWhiteSpace(id))
            {
                featureIds.Add(id);
            }
        }
    }

    private static void ExtractFeatureConstants(string filePath, HashSet<string> featureIds, string prefix)
    {
        var content = File.ReadAllText(filePath);
        var matches = ConstantStringRegex().Matches(content);

        foreach (Match match in matches)
        {
            var value = match.Groups[1].Value;

            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                !value.Contains(' ') &&
                value.Contains('.'))
            {
                featureIds.Add(value);
            }
        }
    }

    private static string? FindOrchardCoreSource(string repositoryRoot)
    {
        // Check environment variable first.
        var envPath = Environment.GetEnvironmentVariable(_orchardCoreSourceEnvVar);
        if (!string.IsNullOrWhiteSpace(envPath) && Directory.Exists(envPath))
        {
            return envPath;
        }

        // Search common locations relative to the repository root.
        var parentDir = Directory.GetParent(repositoryRoot)?.FullName;
        if (parentDir == null)
        {
            return null;
        }

        var grandParentDir = Directory.GetParent(parentDir)?.FullName;

        var candidatePaths = new List<string>
        {
            // Sibling directory patterns.
            Path.Combine(parentDir, "OrchardCore"),
            Path.Combine(parentDir, "OrchardCMS", "OrchardCore"),
        };

        if (grandParentDir != null)
        {
            // One level up, then into OrchardCMS.
            candidatePaths.Add(Path.Combine(grandParentDir, "OrchardCMS", "OrchardCore"));
            candidatePaths.Add(Path.Combine(grandParentDir, "OrchardCore"));
        }

        foreach (var candidate in candidatePaths)
        {
            if (Directory.Exists(candidate) &&
                Directory.Exists(Path.Combine(candidate, "src", "OrchardCore.Modules")))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string[] GetKnownOrchardCoreFeatureIds()
    {
        return
        [
            "OrchardCore.Admin",
            "OrchardCore.AdminDashboard",
            "OrchardCore.AdminMenu",
            "OrchardCore.AdminTemplates",
            "OrchardCore.Alias",
            "OrchardCore.Apis.GraphQL",
            "OrchardCore.ArchiveLater",
            "OrchardCore.AuditTrail",
            "OrchardCore.Autoroute",
            "OrchardCore.AutoSetup",
            "OrchardCore.Azure",
            "OrchardCore.AzureAI",
            "OrchardCore.BackgroundTasks",
            "OrchardCore.ContentFields",
            "OrchardCore.ContentFields.Indexing.SQL",
            "OrchardCore.ContentFields.Indexing.SQL.UserPicker",
            "OrchardCore.ContentLocalization",
            "OrchardCore.ContentLocalization.ContentCulturePicker",
            "OrchardCore.ContentLocalization.Sitemaps",
            "OrchardCore.ContentPreview",
            "OrchardCore.Contents",
            "OrchardCore.Contents.Deployment.AddToDeploymentPlan",
            "OrchardCore.Contents.Deployment.Download",
            "OrchardCore.Contents.Deployment.ExportContentToDeploymentTarget",
            "OrchardCore.Contents.FileContentDefinition",
            "OrchardCore.ContentTransfer",
            "OrchardCore.ContentTypes",
            "OrchardCore.Cors",
            "OrchardCore.CustomSettings",
            "OrchardCore.DataLocalization",
            "OrchardCore.DataOrchestrator",
            "OrchardCore.DataProtection.Azure",
            "OrchardCore.Demo",
            "OrchardCore.Demo.Foo",
            "OrchardCore.Deployment",
            "OrchardCore.Deployment.Remote",
            "OrchardCore.Diagnostics",
            "OrchardCore.DynamicCache",
            "OrchardCore.Elasticsearch",
            "OrchardCore.Email",
            "OrchardCore.Email.Azure",
            "OrchardCore.Email.Smtp",
            "OrchardCore.Facebook",
            "OrchardCore.Facebook.Login",
            "OrchardCore.Facebook.Pixel",
            "OrchardCore.Facebook.Widgets",
            "OrchardCore.Features",
            "OrchardCore.Feeds",
            "OrchardCore.Flows",
            "OrchardCore.Forms",
            "OrchardCore.GitHub.Authentication",
            "OrchardCore.Google.Analytics",
            "OrchardCore.Google.GoogleAuthentication",
            "OrchardCore.Google.TagManager",
            "OrchardCore.HealthChecks",
            "OrchardCore.HomeRoute",
            "OrchardCore.Html",
            "OrchardCore.Https",
            "OrchardCore.Indexing",
            "OrchardCore.Indexing.Worker",
            "OrchardCore.Layers",
            "OrchardCore.Liquid",
            "OrchardCore.Liquid.Core",
            "OrchardCore.Lists",
            "OrchardCore.Localization",
            "OrchardCore.Localization.AdminCulturePicker",
            "OrchardCore.Localization.ContentLanguageHeader",
            "OrchardCore.Lucene",
            "OrchardCore.Markdown",
            "OrchardCore.Media",
            "OrchardCore.Media.AmazonS3",
            "OrchardCore.Media.AmazonS3.ImageSharpImageCache",
            "OrchardCore.Media.Azure.ImageSharpImageCache",
            "OrchardCore.Media.Azure.Storage",
            "OrchardCore.Media.Cache",
            "OrchardCore.Media.Indexing",
            "OrchardCore.Media.Indexing.OpenXML",
            "OrchardCore.Media.Indexing.Pdf",
            "OrchardCore.Media.Indexing.Text",
            "OrchardCore.Media.Security",
            "OrchardCore.Media.Slugify",
            "OrchardCore.Menu",
            "OrchardCore.Microsoft.Authentication.AzureAD",
            "OrchardCore.Microsoft.Authentication.MicrosoftAccount",
            "OrchardCore.MiniProfiler",
            "OrchardCore.Navigation",
            "OrchardCore.Notifications",
            "OrchardCore.Notifications.Email",
            "OrchardCore.Notifications.Sms",
            "OrchardCore.OpenId",
            "OrchardCore.OpenId.Client",
            "OrchardCore.OpenId.Management",
            "OrchardCore.OpenId.Server",
            "OrchardCore.OpenId.Validation",
            "OrchardCore.Placements",
            "OrchardCore.Placements.FileStorage",
            "OrchardCore.PublishLater",
            "OrchardCore.Queries",
            "OrchardCore.Queries.Core",
            "OrchardCore.Queries.Sql",
            "OrchardCore.ReCaptcha",
            "OrchardCore.ReCaptcha.Users",
            "OrchardCore.Recipes",
            "OrchardCore.Recipes.Core",
            "OrchardCore.Redis",
            "OrchardCore.Redis.Bus",
            "OrchardCore.Redis.Cache",
            "OrchardCore.Redis.DataProtection",
            "OrchardCore.Redis.Lock",
            "OrchardCore.RemotePublishing",
            "OrchardCore.Resources",
            "OrchardCore.ResponseCompression",
            "OrchardCore.ReverseProxy",
            "OrchardCore.Roles",
            "OrchardCore.Roles.Core",
            "OrchardCore.Rules",
            "OrchardCore.Scripting",
            "OrchardCore.Search",
            "OrchardCore.Search.AzureAI",
            "OrchardCore.Search.Elasticsearch",
            "OrchardCore.Search.Elasticsearch.ContentPicker",
            "OrchardCore.Search.Elasticsearch.Worker",
            "OrchardCore.Search.Lucene",
            "OrchardCore.Search.Lucene.ContentPicker",
            "OrchardCore.Search.Lucene.Worker",
            "OrchardCore.Security",
            "OrchardCore.Seo",
            "OrchardCore.Settings",
            "OrchardCore.Setup",
            "OrchardCore.Shortcodes",
            "OrchardCore.Shortcodes.Templates",
            "OrchardCore.Sitemaps",
            "OrchardCore.Sitemaps.Cleanup",
            "OrchardCore.Sitemaps.RazorPages",
            "OrchardCore.Sms",
            "OrchardCore.Sms.Azure",
            "OrchardCore.Spatial",
            "OrchardCore.Taxonomies",
            "OrchardCore.Taxonomies.ContentsAdminList",
            "OrchardCore.Templates",
            "OrchardCore.Tenants",
            "OrchardCore.Tenants.Distributed",
            "OrchardCore.Tenants.FeatureProfiles",
            "OrchardCore.Tenants.FileProvider",
            "OrchardCore.Themes",
            "OrchardCore.Title",
            "OrchardCore.Twitter",
            "OrchardCore.Twitter.Signin",
            "OrchardCore.UrlRewriting",
            "OrchardCore.Users",
            "OrchardCore.Users.2FA",
            "OrchardCore.Users.2FA.AuthenticatorApp",
            "OrchardCore.Users.2FA.Email",
            "OrchardCore.Users.2FA.Sms",
            "OrchardCore.Users.AuditTrail",
            "OrchardCore.Users.Authentication.CacheTicketStore",
            "OrchardCore.Users.ChangeEmail",
            "OrchardCore.Users.CustomUserSettings",
            "OrchardCore.Users.ExternalAuthentication",
            "OrchardCore.Users.Localization",
            "OrchardCore.Users.Registration",
            "OrchardCore.Users.ResetPassword",
            "OrchardCore.Users.TimeZone",
            "OrchardCore.Widgets",
            "OrchardCore.Workflows",
            "OrchardCore.Workflows.Http",
            "OrchardCore.Workflows.Session",
            "OrchardCore.Workflows.Timers",
            "OrchardCore.XmlRpc",
        ];
    }

    private static string[] GetKnownOrchardCoreThemeIds()
    {
        return
        [
            "SafeMode",
            "TheAdmin",
            "TheAgencyTheme",
            "TheBlogTheme",
            "TheComingSoonTheme",
            "TheTheme",
        ];
    }

    [GeneratedRegex("""Id\s*=\s*"([^"]+)"\s*[,\)]""")]
    private static partial Regex IdAssignmentRegex();

    [GeneratedRegex("""const\s+string\s+\w+\s*=\s*"([^"]+)"\s*;""")]
    private static partial Regex ConstantStringRegex();

    [GeneratedRegex("""(?<modifiers>[^\n{};]*)\bclass\s+(?<name>\w+)(?:<[^>]*>)?\s*:\s*(?<bases>[^{]+)\{""")]
    private static partial Regex ActivityClassRegex();

    [GeneratedRegex("new(?:\\s+Permission)?\\s*\\(\\s*\"([^\"]+)\"")]
    private static partial Regex PermissionNameRegex();
}
