using System.Text;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// A file past about eight hundred lines has stopped being one thing. Nobody reads it end to end, so a change is
/// made by finding the nearest similar-looking block and copying it, which is how the four duplicated HTTP
/// helpers and the three roll-up implementations in this codebase came to exist.
/// <para>
/// This is a ratchet, not a bar: the files that are already too long are listed with their current size, and the
/// test fails when a listed file grows or a new one crosses the line. Failing on the existing ones would mean
/// either a refactor of each before any unrelated change could land, or the whole rule being suppressed — and a
/// suppressed rule stops anybody noticing the next one.
/// </para>
/// <para>
/// It began with the Contact Center, Telephony and Omnichannel projects and now reads every C# file under
/// <c>src</c> (CQ-10 in <c>docs/engineering/review-2026-11/04-code-quality.md</c>).
/// </para>
/// </summary>
public sealed class FileSizeRatchetTests
{
    /// <summary>
    /// The line count past which a file is expected to be split.
    /// </summary>
    private const int MaximumLines = 800;

    /// <summary>
    /// The files that were already over the line when the ratchet was set, and the size each was at. A file may
    /// shrink freely — lowering its number here is the point — but it may not grow.
    /// </summary>
    private static readonly Dictionary<string, int> _existing = new(StringComparer.OrdinalIgnoreCase)
    {
        ["src/Abstractions/CrestApps.OrchardCore.Dispatch.Abstractions/Models/FieldModels.cs"] = 809,
        ["src/Core/CrestApps.OrchardCore.AI.Core/Services/DataSourceIndexingService.cs"] = 1358,
        ["src/Core/CrestApps.OrchardCore.AgenticCommerce.Core/Services/AgentCheckoutService.cs"] = 1642,
        ["src/Core/CrestApps.OrchardCore.Calendars.Core/Services/DefaultCalendarEventManager.cs"] = 1035,
        ["src/Core/CrestApps.OrchardCore.Carts.Core/Services/CartService.cs"] = 814,
        ["src/Core/CrestApps.OrchardCore.Checkout.Core/Services/DefaultCheckoutEngine.cs"] = 1632,
        ["src/Core/CrestApps.OrchardCore.Reports.Core/Designer/ReportDesignDocumentBuilder.cs"] = 1036,
        ["src/Core/CrestApps.OrchardCore.Reports.Core/Designer/ReportQueryEngine.cs"] = 1050,
        ["src/Core/CrestApps.OrchardCore.Scheduling.Core/Services/DefaultAppointmentService.cs"] = 845,
        ["src/Core/CrestApps.OrchardCore.Subscriptions.Core/Services/DefaultInstallmentPlanService.cs"] = 1571,
        ["src/Core/CrestApps.OrchardCore.TenantHierarchy.Core/Services/TenantHierarchyBroker.cs"] = 1126,
        ["src/Core/CrestApps.OrchardCore.WorkOrders.Core/Services/WorkOrderService.cs"] = 804,
        ["src/Core/CrestApps.OrchardCore.Workforce.Core/Services/DefaultSchedulePlanningService.cs"] = 814,
        ["src/Modules/CrestApps.OrchardCore.Asterisk/Services/AsteriskAriClient.cs"] = 930,
        ["src/Modules/CrestApps.OrchardCore.Asterisk/Services/AsteriskTelephonyProviderBase.cs"] = 1348,
        ["src/Modules/CrestApps.OrchardCore.ContactCenter/Reports/Providers/EnterpriseInteractionReportProvider.cs"] = 1148,
        ["src/Modules/CrestApps.OrchardCore.ContentTransfer/BackgroundTasks/ImportFilesBackgroundTask.cs"] = 1630,
        ["src/Modules/CrestApps.OrchardCore.ContentTransfer/Controllers/AdminController.cs"] = 1590,
        ["src/Modules/CrestApps.OrchardCore.DncRegistry/Services/DefaultLocalDncListManager.cs"] = 1259,
        ["src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Controllers/ActivitiesController.cs"] = 2223,
        ["src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Reports/EnterpriseActivityReportProvider.cs"] = 891,
        ["src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Services/DefaultContactActivityBatchLoader.cs"] = 1367,
        ["src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Services/LeadConversionService.cs"] = 906,
        ["src/Modules/CrestApps.OrchardCore.Omnichannel.Sms/Handlers/SmsOmnichannelEventHandler.cs"] = 1478,
        ["src/Modules/CrestApps.OrchardCore.Products/Services/ProductListingService.cs"] = 1509,
        ["src/Modules/CrestApps.OrchardCore.Recipes/Startup.cs"] = 1894,
        ["src/Modules/CrestApps.OrchardCore.Reports/Designer/Services/ReportDesignRunner.cs"] = 964,
        ["src/Modules/CrestApps.OrchardCore.Stripe/Endpoints/CreateWebhookEndpoint.cs"] = 871,
        ["src/Modules/CrestApps.OrchardCore.Stripe/Services/StripeCheckoutPaymentProvider.cs"] = 1024,
        ["src/Modules/CrestApps.OrchardCore.Telephony/Hubs/TelephonyHub.cs"] = 1237,
        ["src/Modules/CrestApps.OrchardCore.TenantHierarchy/Controllers/ChildTenantsController.cs"] = 893,
        ["src/Modules/CrestApps.OrchardCore.Transactions/Services/DefaultTransactionCommandService.cs"] = 877,
        ["src/Modules/CrestApps.OrchardCore.WorkOrders/Controllers/WorkOrderSettingsController.cs"] = 874,
        ["src/Modules/CrestApps.OrchardCore.Workforce/Controllers/MyWorkController.cs"] = 1153,
        ["src/Modules/CrestApps.OrchardCore.Workforce/Controllers/SchedulesController.cs"] = 979,
    };

    [Fact]
    public void NoNewFile_CrossesTheSizeLine_AndNoListedFileGrows()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var violations = new List<string>();

        // Act
        foreach (var (relativePath, lines) in EnumerateSourceFiles(repositoryRoot))
        {
            if (_existing.TryGetValue(relativePath, out var recorded))
            {
                if (lines > recorded)
                {
                    violations.Add($"{relativePath} grew from {recorded} to {lines} lines. Split it rather than adding to it.");
                }

                continue;
            }

            if (lines > MaximumLines)
            {
                violations.Add($"{relativePath} is {lines} lines, over the {MaximumLines}-line limit. Split it, or add it to the ratchet with a reason.");
            }
        }

        // Assert
        Assert.True(violations.Count == 0, BuildMessage(violations));
    }

    [Fact]
    public void EveryRatchetedFile_StillExists_AndIsStillOverTheLine()
    {
        // Arrange
        // A ratchet nobody prunes becomes a list of files that were long in 2026. When one is split or deleted,
        // its entry has to go, or the limit quietly stops applying to that path forever.
        var repositoryRoot = FindRepositoryRoot();
        var actual = EnumerateSourceFiles(repositoryRoot).ToDictionary(file => file.RelativePath, file => file.Lines, StringComparer.OrdinalIgnoreCase);
        var stale = new List<string>();

        // Act
        foreach (var (relativePath, recorded) in _existing)
        {
            if (!actual.TryGetValue(relativePath, out var lines))
            {
                stale.Add($"{relativePath} no longer exists; remove it from the ratchet.");
            }
            else if (lines <= MaximumLines)
            {
                stale.Add($"{relativePath} is now {lines} lines and no longer needs an exception; remove it from the ratchet.");
            }
            else if (lines < recorded)
            {
                stale.Add($"{relativePath} shrank from {recorded} to {lines} lines; lower its recorded size so the gain is locked in.");
            }
        }

        // Assert
        Assert.True(stale.Count == 0, BuildMessage(stale));
    }

    private static string BuildMessage(IReadOnlyList<string> lines)
    {
        var message = new StringBuilder().AppendLine();

        foreach (var line in lines.Order(StringComparer.Ordinal))
        {
            message.Append("    ").AppendLine(line);
        }

        return message.ToString();
    }

    private static IEnumerable<(string RelativePath, int Lines)> EnumerateSourceFiles(string repositoryRoot)
    {
        var scanned = 0;

        foreach (var file in SourceTreeFiles.Enumerate(Path.Combine(repositoryRoot, "src"), "*.cs", IsSkippedDirectory))
        {
            scanned++;

            yield return (
                Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/'),
                File.ReadAllLines(file).Length);
        }

        Assert.True(scanned > 5000, $"Only {scanned} source files were scanned; the scan no longer matches the repository layout.");
    }

    private static bool IsSkippedDirectory(string directory)
        => SourceTreeFiles.IsBuildOutputDirectory(directory) || Path.GetFileName(directory) is "node_modules" or "wwwroot";

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
