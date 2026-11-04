using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Stops new copies of the patterns that the November 2026 reusability audit lists as duplicates (see
/// <c>docs/engineering/reusability-audit-2026-11.md</c>, sections 2 and 6). Each pattern names the building block to use
/// instead. The files that held a copy when the ratchet was set are listed with the number of copies each had; a listed
/// file may lose copies but never gain one, and no other file may add one.
/// </summary>
public sealed class DuplicatePatternRatchetTests
{
    private static readonly DuplicatePattern[] _patterns =
    [
        new(
            "EscapeCsv",
            @"private\s+(?:static\s+)?[\w<>?]+\s+EscapeCsv\w*\s*\(",
            "Write CSV with CrestApps.OrchardCore.Core's CsvWriter, which escapes quotes, commas and line breaks and guards against formula injection."),
        new(
            "new BlobFileStore",
            @"\bnew\s+BlobFileStore\s*\(",
            "Register tenant file storage through the shared tenant file store helper instead of another .Azure module."),
        new(
            "DeploymentSerializer class",
            @"\bclass\s+\w*DeploymentSerializer\b",
            "Use CatalogDeploymentSerializer (or ProtectedSettingsDeploymentSerializer) from CrestApps.OrchardCore.Core.",
            ExemptDirectory: "src/Core/CrestApps.OrchardCore.Core/"),
        new(
            "GetTimeZones() in a select list",
            @"\bGetTimeZones\s*\(\s*\)",
            "Inject Orchard Core's ITimeZoneSelectListProvider to build a time-zone picker.",
            RequiredFileText: "SelectListItem"),
        new(
            "DateTime.UtcNow, DateTime.Now, DateTime.Today or DateTimeOffset.UtcNow",
            @"\bDateTime\.(?:UtcNow|Now|Today)\b|\bDateTimeOffset\.UtcNow\b",
            "Inject IClock (or TimeProvider/Stopwatch for latency timers) so the time can be tested and is never server-local."),
        new(
            "TimeZoneInfo.ConvertTimeToUtc",
            @"\bTimeZoneInfo\.ConvertTimeToUtc\b",
            "Convert wall-clock times with CalendarTime, which resolves daylight-saving gaps instead of throwing."),
        new(
            "private FixedTimeEquals",
            @"private\s+(?:static\s+)?bool\s+FixedTimeEquals\s*\(",
            "Call CryptographicOperations.FixedTimeEquals."),
    ];

    /// <summary>
    /// The copies that existed when the ratchet was set, per pattern and file. Never add an entry or raise a count: use
    /// the building block the pattern names. Lower a count or delete an entry as soon as a copy is removed.
    /// </summary>
    private static readonly Dictionary<string, Dictionary<string, int>> _knownCopies = new(StringComparer.Ordinal)
    {
        ["EscapeCsv"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["src/Modules/CrestApps.OrchardCore.AI.Chat/Controllers/ChatAnalyticsController.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.AI.Chat/Controllers/ChatConversionGoalsController.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.AI.Chat/Controllers/ChatExtractedDataController.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.DncRegistry/Controllers/LocalDncRegistryAdminController.cs"] = 1,
        },
        ["new BlobFileStore"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["src/Modules/CrestApps.OrchardCore.AI.Documents.Azure/Startup.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.ContentTransfer.Azure/Startup.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.DncRegistry.Azure/Startup.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging.Azure/Startup.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Telephony.Azure/Startup.cs"] = 1,
        },
        ["DeploymentSerializer class"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["src/Modules/CrestApps.OrchardCore.ContactCenter/Deployments/ContactCenterDeploymentSerializer.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Deployments/OmnichannelDeploymentSerializer.cs"] = 1,
        },
        ["GetTimeZones() in a select list"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["src/Modules/CrestApps.OrchardCore.Calendars/Services/CalendarSelectLists.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.ContactCenter/Drivers/BusinessHoursCalendarDisplayDriver.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Geo/Controllers/TerritoriesController.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Scheduling/Controllers/SchedulingResourcesController.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Teams/Drivers/TeamDisplayDriver.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.TimeZones/Drivers/TimeZoneMapDisplayDriver.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.WorkOrders/Agreements/ServiceAgreementsController.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.WorkOrders/Controllers/WorkOrderSettingsController.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Workforce/Controllers/WorkersController.cs"] = 1,
        },
        ["DateTime.UtcNow, DateTime.Now, DateTime.Today or DateTimeOffset.UtcNow"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["src/Core/CrestApps.OrchardCore.AI.Core/Conversations/AIConversationTurnNotifier.cs"] = 1,
            ["src/Core/CrestApps.OrchardCore.Core/Services/GuestSessionTokenManager.cs"] = 1,
            ["src/Core/CrestApps.OrchardCore.DataConnectors.Core/Http/DataConnectorHttpClient.cs"] = 1,
            ["src/Core/CrestApps.OrchardCore.Omnichannel.Core/Services/InMemoryAutomatedConversationGate.cs"] = 1,
            ["src/Core/CrestApps.OrchardCore.Omnichannel.Voice.Core/Services/AIVoiceSessionMeter.cs"] = 1,
            ["src/Core/CrestApps.OrchardCore.Omnichannel.Voice.Core/Services/RealtimeVoiceConversationRunner.BargeIn.cs"] = 1,
            ["src/Core/CrestApps.OrchardCore.Omnichannel.Voice.Core/Services/RealtimeVoiceConversationRunner.cs"] = 7,
            ["src/Core/CrestApps.OrchardCore.Omnichannel.Voice.Core/Services/RealtimeVoiceConversationRunner.Guardrails.cs"] = 3,
            ["src/Core/CrestApps.OrchardCore.Omnichannel.Voice.Core/Services/RealtimeVoiceConversationRunner.Handover.cs"] = 4,
            ["src/Core/CrestApps.OrchardCore.Omnichannel.Voice.Core/Services/RealtimeVoiceConversationRunner.Pumps.cs"] = 9,
            ["src/Core/CrestApps.OrchardCore.Omnichannel.Voice.Core/Services/RealtimeVoiceConversationRunner.Session.cs"] = 2,
            ["src/Core/CrestApps.OrchardCore.Omnichannel.Voice.Core/Services/RealtimeVoiceConversationRunner.Supervision.cs"] = 2,
            ["src/Core/CrestApps.OrchardCore.Omnichannel.Voice.Core/Services/RealtimeVoiceConversationRunner.UnheardReply.cs"] = 5,
            ["src/Core/CrestApps.OrchardCore.Omnichannel.Voice.Core/Services/RealtimeVoiceConversationRunner.Voicemail.cs"] = 1,
            ["src/Core/CrestApps.OrchardCore.Omnichannel.Voice.Core/Services/VoiceAgentConversationLoop.Usage.cs"] = 1,
            ["src/Core/CrestApps.OrchardCore.Reports.Core/Designer/ReportColumnColorizer.cs"] = 1,
            ["src/Core/CrestApps.OrchardCore.Telnyx.Core/Services/SoftPhoneHealthMetrics.cs"] = 1,
            ["src/Core/CrestApps.OrchardCore.Telnyx.Core/Services/TelnyxContactCenterVoiceMediaSession.cs"] = 6,
            ["src/Core/CrestApps.OrchardCore.Telnyx.Core/Services/TelnyxContactCenterVoiceProvider.Monitoring.cs"] = 2,
            ["src/Core/CrestApps.OrchardCore.Telnyx.Core/Services/TelnyxTransferCommands.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.AI/Handlers/ChatConversationStartingOrchestrationHandler.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.ContactCenter/Migrations/EntryPointAddressMigrations.cs"] = 2,
            ["src/Modules/CrestApps.OrchardCore.Geo.AzureMaps/AzureMapsProvider.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Geo.GoogleMaps/GoogleMapsProvider.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Controllers/ActivityBatchesController.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Services/DefaultActivityBatchLoadCoordinator.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Services/LeadImportProvider.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging/Migrations/MessagingEntryPointMigrations.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Telephony/Hubs/TelephonyHub.cs"] = 2,
            ["src/Modules/CrestApps.OrchardCore.Workforce/Controllers/PayPeriodsController.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.WorkOrders/Integrations/GeoWorkOrders.cs"] = 1,
        },
        ["TimeZoneInfo.ConvertTimeToUtc"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["src/Core/CrestApps.OrchardCore.WorkOrders.Core/Services/SlaClock.cs"] = 2,
            ["src/Modules/CrestApps.OrchardCore.Bookings/Calendar/BookingCalendarSlotFilter.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Bookings/Services/BookingDepositService.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Bookings/Services/BookingSchedule.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.ContactCenter/Reports/Services/ReportTimeZone.cs"] = 2,
            ["src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Migrations/OmnichannelActivityBatchScheduleMigrations.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Orders/Dashboard/SalesSummaryWidget.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Reports/Designer/Services/ReportExecutionContextFactory.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Reports/Subscriptions/Services/ReportSubscriptionSchedule.cs"] = 1,
        },
        ["private FixedTimeEquals"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["src/Modules/CrestApps.OrchardCore.AI.A2A/Handlers/A2AApiKeyAuthenticationHandler.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.AI.Mcp/Handlers/McpApiKeyAuthenticationHandler.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Omnichannel.EventGrid/Endpoints/AzureEventGridEndpoint.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.TenantHierarchy/Controllers/DelegatedEntryController.cs"] = 1,
        },
    };

    private static readonly Lazy<Dictionary<string, Dictionary<string, int>>> _actualCopies = new(ScanSources);

    [Fact]
    public void NoFile_AddsACopyOfAKnownDuplicate()
    {
        // Arrange
        var added = new List<string>();

        // Act
        foreach (var pattern in _patterns)
        {
            var known = _knownCopies.GetValueOrDefault(pattern.Name) ?? [];

            foreach (var (file, count) in _actualCopies.Value[pattern.Name])
            {
                var allowed = known.GetValueOrDefault(file);

                if (count > allowed)
                {
                    added.Add($"{file}: {count} copies of '{pattern.Name}' (allowed {allowed}). {pattern.Instead}");
                }
            }
        }

        // Assert
        Assert.True(added.Count == 0, BuildMessage(
            "These files add a copy of a pattern that the reusability audit lists as a duplicate. Use the building block named instead:",
            added));
    }

    [Fact]
    public void EveryKnownCopy_StillExists()
    {
        // Arrange
        var stale = new List<string>();

        // Act
        foreach (var (patternName, known) in _knownCopies)
        {
            Assert.True(_actualCopies.Value.ContainsKey(patternName), $"'{patternName}' in _knownCopies names no pattern.");

            var actual = _actualCopies.Value[patternName];

            foreach (var (file, allowed) in known)
            {
                var count = actual.GetValueOrDefault(file);

                if (count == 0)
                {
                    stale.Add($"{file} has no copy of '{patternName}' any more. Delete its entry.");
                }
                else if (count < allowed)
                {
                    stale.Add($"{file} has {count} copies of '{patternName}', not {allowed}. Lower its count to {count}.");
                }
            }
        }

        // Assert
        Assert.True(stale.Count == 0, BuildMessage(
            $"Copies were removed. Update {nameof(DuplicatePatternRatchetTests)}._knownCopies so they cannot come back:",
            stale));
    }

    [Theory]
    [InlineData("EscapeCsv", "    private static string EscapeCsv(string value)", 1)]
    [InlineData("EscapeCsv", "    private static string EscapeCsvField(string value)", 1)]
    [InlineData("EscapeCsv", "    builder.Append(EscapeCsv(value));", 0)]
    [InlineData("DateTime.UtcNow, DateTime.Now, DateTime.Today or DateTimeOffset.UtcNow", "var now = DateTime.UtcNow; var today = DateTime.Today;", 2)]
    [InlineData("DateTime.UtcNow, DateTime.Now, DateTime.Today or DateTimeOffset.UtcNow", "var now = DateTimeOffset.UtcNow;", 1)]
    [InlineData("DateTime.UtcNow, DateTime.Now, DateTime.Today or DateTimeOffset.UtcNow", "// Never call DateTime.UtcNow here.", 0)]
    [InlineData("DateTime.UtcNow, DateTime.Now, DateTime.Today or DateTimeOffset.UtcNow", "var now = _clock.UtcNow;", 0)]
    [InlineData("private FixedTimeEquals", "    private static bool FixedTimeEquals(string a, string b)", 1)]
    [InlineData("private FixedTimeEquals", "    return CryptographicOperations.FixedTimeEquals(a, b);", 0)]
    public void Count_MatchesDeclarationsAndCallsButNotComments(string patternName, string line, int expected)
    {
        // Arrange
        var pattern = _patterns.Single(candidate => candidate.Name == patternName);

        // Act
        var actual = pattern.Count([line]);

        // Assert
        Assert.Equal(expected, actual);
    }

    private static Dictionary<string, Dictionary<string, int>> ScanSources()
    {
        var repositoryRoot = ProjectReferenceRulesTests.FindRepositoryRoot();
        var result = _patterns.ToDictionary(
            pattern => pattern.Name,
            _ => new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            StringComparer.Ordinal);
        var scanned = 0;

        foreach (var file in Directory.EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (IsExcluded(file))
            {
                continue;
            }

            scanned++;

            var relativePath = Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/');
            var lines = File.ReadAllLines(file);
            string text = null;

            foreach (var pattern in _patterns)
            {
                if (pattern.ExemptDirectory is not null && relativePath.StartsWith(pattern.ExemptDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (pattern.RequiredFileText is not null)
                {
                    text ??= string.Join('\n', lines);

                    if (!text.Contains(pattern.RequiredFileText, StringComparison.Ordinal))
                    {
                        continue;
                    }
                }

                var count = pattern.Count(lines);

                if (count > 0)
                {
                    result[pattern.Name][relativePath] = count;
                }
            }
        }

        Assert.True(scanned > 5000, $"Only {scanned} source files were scanned; the scan no longer matches the repository layout.");

        return result;
    }

    private static bool IsExcluded(string path)
    {
        foreach (var folder in new[] { "bin", "obj", "node_modules" })
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}{folder}{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildMessage(string header, IEnumerable<string> lines)
    {
        var message = new StringBuilder().AppendLine(header);

        foreach (var line in lines.Order(StringComparer.Ordinal))
        {
            message.Append("    ").AppendLine(line);
        }

        return message.ToString();
    }

    private sealed record DuplicatePattern(string Name, string Expression, string Instead, string ExemptDirectory = null, string RequiredFileText = null)
    {
        private readonly Regex _regex = new(Expression, RegexOptions.Compiled);

        public int Count(IEnumerable<string> lines)
        {
            var count = 0;

            foreach (var line in lines)
            {
                var code = line.TrimStart();

                // Comments and XML documentation may name a pattern to warn against it.
                if (code.StartsWith("//", StringComparison.Ordinal) || code.StartsWith('*'))
                {
                    continue;
                }

                count += _regex.Count(line);
            }

            return count;
        }
    }
}
