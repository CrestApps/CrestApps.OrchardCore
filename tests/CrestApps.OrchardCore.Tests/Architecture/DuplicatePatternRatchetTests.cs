using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Stops new copies of the patterns that the November 2026 reusability audit lists as duplicates (see
/// <c>docs/engineering/reusability-audit-2026-11.md</c>, sections 2 and 6). Each pattern names the building block to use
/// instead. The files that held a copy when the ratchet was set are listed with the number of copies each had; a listed
/// file may lose copies but never gain one, and no other file may add one.
/// <para>
/// Each file is matched as a whole, with its comments blanked, so a call split over several lines is seen as well.
/// </para>
/// </summary>
public sealed class DuplicatePatternRatchetTests
{
    private static readonly DuplicatePattern[] _patterns =
    [
        new(
            "EscapeCsv",
            @"private\s+(?:static\s+)?[\w<>?]+\s+EscapeCsv\w*\s*\(",
            "Write CSV with CsvWriter (CrestApps.OrchardCore.Tabular, in CrestApps.OrchardCore.Abstractions), which escapes quotes, commas and line breaks and guards against formula injection."),
        new(
            "new BlobFileStore",
            @"\bnew\s+BlobFileStore\s*\(",
            "Register tenant file storage with services.AddTenantFileStore<TStore>(...) from CrestApps.OrchardCore.FileStorage.Core instead of another .Azure module.",
            ExemptDirectory: "src/Core/CrestApps.OrchardCore.FileStorage.Core/"),
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
            "GetAwaiter().GetResult()",
            @"\.GetAwaiter\s*\(\s*\)\s*\.GetResult\s*\(\s*\)",
            "Await the task: make the caller async, or use the async overload of the hook (OnDisplaying, InvokeAsync, GetColumnsAsync). Blocking on a task ties up a thread and can deadlock."),
        new(
            "private FixedTimeEquals",
            @"private\s+(?:static\s+)?bool\s+FixedTimeEquals\s*\(",
            "Call CryptographicOperations.FixedTimeEquals."),
        new(
            "Status429TooManyRequests",
            @"\bStatus429TooManyRequests\b",
            "Register an attempt policy (IAttemptLimiter) or a group limit (AddClientGroupRateLimit with AddJsonRejectionHandler), and answer with ThrottlingServiceCollectionExtensions.WriteTooManyRequestsAsync or ApiProblems.TooManyRequests.",
            ExemptDirectory: "src/Core/CrestApps.OrchardCore.Core/Throttling/"),
        new(
            "RemoteIpAddress?.ToString()",
            @"RemoteIpAddress\s*\??\s*\.\s*ToString\s*\(\s*\)",
            "Key a limit with ClientNetworkKey (NetworkOnly, From or FromAddress), which masks IPv6 to its /64 and honors the Reverse Proxy feature.",
            ExemptDirectory: "src/Core/CrestApps.OrchardCore.Core/Throttling/"),
        new(
            "HMAC signature verified by hand",
            @"\bHMACSHA(?:1|256|384|512)\b",
            "Check a provider's signature with HmacWebhookSignatureValidator (an IWebhookSignatureValidator in CrestApps.OrchardCore.Webhooks.Core), which computes and compares in constant time (DRY-7).",
            ExemptDirectory: "src/Core/CrestApps.OrchardCore.Webhooks.Core/",
            RequiredFileText: "FixedTimeEquals"),
        new(
            "secret token made and hashed by hand",
            @"\bSHA256\.HashData\s*\(",
            "Make a secret token with SecretTokens.Create and hash it with SecretTokens.Hash (CrestApps.OrchardCore.Core.Security) (DRY-2).",
            ExemptDirectory: "src/Core/CrestApps.OrchardCore.Core/Security/",
            RequiredFileText: "RandomNumberGenerator"),
        new(
            "time-limited protector by hand",
            @"\.ToTimeLimitedDataProtector\s*\(",
            "Sign a time-limited value with SignedTokenService (CrestApps.OrchardCore.Core.Services), with a purpose of its own (DRY-2).",
            ExemptDirectory: "src/Core/CrestApps.OrchardCore.Core/Services/SignedTokenService.cs"),
        new(
            "data protector outside Core",
            @"\.CreateProtector\s*\(",
            "Protect and read a stored secret with ProtectSetting and UnprotectSetting (ProtectedSettingExtensions in CrestApps.OrchardCore.Core.Security), and sign a value with SignedTokenService, each with a purpose of its own (DRY-12a, DRY-2).",
            ExemptDirectory: "src/Core/CrestApps.OrchardCore.Core/"),
        new(
            "Untracked Task.Run",
            @"(?:\b_\s*=|=>)\s*Task\s*\.\s*Run\s*\(",
            "Nothing observes a discarded Task.Run: a fault is lost and a shutdown drops the work. Await it, hand it to a tracker that drains on shutdown (AIVoiceCallShutdown.TrackHandOn today), or queue it on ILeasedWorkQueue<T> when it must survive a restart."),
    ];

    /// <summary>
    /// The copies that existed when the ratchet was set, per pattern and file. Never add an entry or raise a count: use
    /// the building block the pattern names. Lower a count or delete an entry as soon as a copy is removed.
    /// </summary>
    private static readonly Dictionary<string, Dictionary<string, int>> _knownCopies = new(StringComparer.Ordinal)
    {
        ["GetTimeZones() in a select list"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["src/Modules/CrestApps.OrchardCore.ContactCenter/Drivers/BusinessHoursCalendarDisplayDriver.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.TimeZones/Drivers/TimeZoneMapDisplayDriver.cs"] = 1,
        },
        ["TimeZoneInfo.ConvertTimeToUtc"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["src/Modules/CrestApps.OrchardCore.ContactCenter/Reports/Services/ReportTimeZone.cs"] = 2,
            ["src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Migrations/OmnichannelActivityBatchScheduleMigrations.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Orders/Dashboard/SalesSummaryWidget.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Reports/Designer/Services/ReportExecutionContextFactory.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Reports/Subscriptions/Services/ReportSubscriptionSchedule.cs"] = 1,
        },
        ["GetAwaiter().GetResult()"] = new(StringComparer.OrdinalIgnoreCase)
        {
            // Orchard's synchronous scripting contract: recipe expressions call renderAITemplate() and read its value.
            ["src/Modules/CrestApps.OrchardCore.AI/Recipes/AITemplateMethodProvider.cs"] = 1,

            // The host's stopping callback is synchronous and has to wait for the drain; the drain tasks complete on the
            // work's own threads, so blocking here cannot deadlock.
            ["src/Modules/CrestApps.OrchardCore.ContactCenter/Services/ContactCenterShutdownDrain.cs"] = 1,
        },
        ["Untracked Task.Run"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["src/Core/CrestApps.OrchardCore.Omnichannel.Voice.Core/Services/AIVoiceCallShutdown.cs"] = 1,
            ["src/Core/CrestApps.OrchardCore.Omnichannel.Voice.Core/Services/AIVoiceSessionTracker.cs"] = 1,
            ["src/Core/CrestApps.OrchardCore.Omnichannel.Voice.Core/Services/RealtimeVoiceConversationRunner.Guardrails.cs"] = 1,
            ["src/Core/CrestApps.OrchardCore.Omnichannel.Voice.Core/Services/RealtimeVoiceConversationRunner.Handover.cs"] = 1,
            ["src/Core/CrestApps.OrchardCore.Omnichannel.Voice.Core/Services/RealtimeVoiceConversationRunner.Supervision.cs"] = 4,
            ["src/Modules/CrestApps.OrchardCore.Omnichannel.Voice/Services/RedisAIVoiceCallMessageBus.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Telnyx/Services/TelnyxAiVoiceRecordingHandler.cs"] = 1,
        },
        ["private FixedTimeEquals"] = new(StringComparer.OrdinalIgnoreCase)
        {
        },
        ["data protector outside Core"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["src/Core/CrestApps.OrchardCore.AgenticCommerce.Core/Services/MerchantSigningKeyService.cs"] = 1,
            ["src/Core/CrestApps.OrchardCore.AI.Chat.Core/Services/ConnectionAddressAIVisitorIdentityResolver.cs"] = 1,
            ["src/Core/CrestApps.OrchardCore.Calendars.Core/Services/CalendarFeedTokens.cs"] = 3,
            ["src/Core/CrestApps.OrchardCore.Commerce.Api.Core/IdempotencyEndpointFilter.cs"] = 1,
            ["src/Core/CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Core/Twilio/TwilioWebhookEndpoint.cs"] = 1,
            ["src/Core/CrestApps.OrchardCore.Omnichannel.Voice.Core/Services/SecureKeypadCaptureStore.cs"] = 1,
            ["src/Core/CrestApps.OrchardCore.Subscriptions.Core/Handlers/UserRegistrationCheckoutHandler.cs"] = 1,
            ["src/Core/CrestApps.OrchardCore.Telnyx.Core/Services/TelnyxSmsOptionsConfiguration.cs"] = 2,
            ["src/Core/CrestApps.OrchardCore.Webhooks.Core/Services/WebhookDispatcher.cs"] = 1,
            ["src/Core/CrestApps.OrchardCore.Workforce.Core/Services/DefaultWebPushService.cs"] = 1,
            ["src/Core/CrestApps.OrchardCore.Workforce.Core/Services/DefaultWorkerFeedService.cs"] = 2,
            ["src/Core/CrestApps.OrchardCore.Workforce.Core/Services/WorkerFeedTokenBackfill.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Accounts.B2B/Services/PunchoutService.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Addresses/GooglePlaces/GooglePlacesAddressAutocompleteProvider.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.AgenticCommerce/Acp/AcpOrderWebhookFormatter.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.AgenticCommerce/Controllers/AgentPlatformsController.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.AI.A2A/Handlers/A2AConnectionSettingsHandler.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.AI.Chat.Claude/ClaudeOptionsConfiguration.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.AI.Chat.Copilot/CopilotOptionsConfiguration.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.AI.DataSources.AzureAI/Services/AzureAISearchAIDataSourceSourceHandler.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.AI.DataSources.Elasticsearch/Services/ElasticsearchAIDataSourceSourceHandler.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.AI.DataSources.PostgreSQL/Services/PostgreSQLAIDataSourceSourceHandler.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.AI/Services/AIWebhookImporter.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.AI/Workflows/Models/SendSignedWebhookTask.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Asterisk/Services/AsteriskAriClient.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Asterisk/Services/AsteriskContactCenterVoiceMediaProvider.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Asterisk/Services/AsteriskPjsipDialogTerminator.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Asterisk/Services/AsteriskRealtimeVoiceTenantEvents.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Asterisk/Services/AsteriskSoftPhoneRegistrationConfigContributor.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Asterisk/Services/AsteriskTelephonyProvider.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.AzureAIInference/Handlers/AzureAIInferenceConnectionHandler.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.AzureAIInference/Handlers/AzureAIInferenceConnectionSettingsHandler.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.ContactCenter/Services/CallRecordingLinks.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.DncRegistry/Services/CanadaDnclRegistry.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.DncRegistry/Services/UsaFtcDncRegistry.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Downloads/Services/DownloadService.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Geo.AzureMaps/AzureMapsProvider.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Geo.GoogleMaps/GoogleMapsProvider.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging.Sms/Services/TwilioSmsMediaSender.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging/Services/MessagingAttachmentLinks.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.OpenAI.Azure/Handlers/AzureOpenAIConnectionHandler.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.OpenAI.Azure/Handlers/AzureOpenAIConnectionSettingsHandler.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.OpenAI/Handlers/OpenAIProviderConnectionHandler.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.OpenAI/Handlers/OpenAIProviderConnectionSettingsHandler.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.PhoneNumbers.Verifications/Services/AbstractApiPhoneNumberVerificationProvider.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.PhoneNumbers.Verifications/Services/TwilioPhoneNumberVerificationProvider.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.PhoneNumbers.Verifications/Services/VeriphonePhoneNumberVerificationProvider.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Telephony/Controllers/TelephonyOAuthController.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Telephony/Services/DefaultTelephonyUserTokenStore.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Telnyx/Controllers/TelnyxConnectController.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Telnyx/Endpoints/TelnyxWebhookEndpoint.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Telnyx/Services/TelnyxOptionsConfigurations.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Wizard/Services/WizardResumeCookieManager.cs"] = 1,
        },
        ["secret token made and hashed by hand"] = new(StringComparer.OrdinalIgnoreCase)
        {
            // A SIP credential's digest, a PKCE challenge and a six-digit one-time code: not secret tokens.
            ["src/Modules/CrestApps.OrchardCore.Asterisk/Services/AsteriskPjsipCredentialIssuer.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Telephony/Services/TelephonyPkceGenerator.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Users/Services/EmailSignInCodes.cs"] = 1,
        },
        ["Status429TooManyRequests"] = new(StringComparer.OrdinalIgnoreCase)
        {
            // The API's problem helpers and the idempotency filter's "already in progress".
            ["src/Core/CrestApps.OrchardCore.Commerce.Api.Core/ApiProblems.cs"] = 1,
            ["src/Core/CrestApps.OrchardCore.Commerce.Api.Core/IdempotencyEndpointFilter.cs"] = 1,

            // The agent protocols' own error shapes.
            ["src/Modules/CrestApps.OrchardCore.AgenticCommerce/Acp/AcpCheckoutOperations.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.AgenticCommerce/Acp/AcpMcpIdempotency.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.AgenticCommerce/Ucp/UcpOperationResult.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.AgenticCommerce/Ucp/UcpCatalogEndpoints.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.AgenticCommerce/Ucp/UcpOperations.cs"] = 1,

            // The payment endpoints answer an attempt policy's refusal in their own JSON shape.
            ["src/Modules/CrestApps.OrchardCore.Checkout/Api/CheckoutApiEndpoints.cs"] = 2,
            ["src/Modules/CrestApps.OrchardCore.Checkout/Endpoints/PaymentEndpoints.cs"] = 2,

            // The provider ingress limiters (a concurrency gate, not an attempt counter).
            ["src/Modules/CrestApps.OrchardCore.ContactCenter/Endpoints/VoiceIngressEndpoint.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.Telnyx/Endpoints/TelnyxWebhookEndpoint.cs"] = 2,
        },
        ["RemoteIpAddress?.ToString()"] = new(StringComparer.OrdinalIgnoreCase)
        {
            // Recorded for an audit trail or a signed request's context, never as a limit's key.
            ["src/Core/CrestApps.OrchardCore.TenantHierarchy.Core/Services/TenantHierarchyBroker.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.AgenticCommerce/Acp/AcpRequestFilter.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.AgenticCommerce/Ucp/UcpRequestFilter.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.TenantHierarchy/Services/ChildTenantManager.cs"] = 1,
            ["src/Modules/CrestApps.OrchardCore.TenantHierarchy/Services/DelegatedAccessIssuer.cs"] = 1,
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
    [InlineData("GetAwaiter().GetResult()", "var value = task.GetAwaiter().GetResult();", 1)]
    [InlineData("GetAwaiter().GetResult()", "var value = await task;", 0)]
    [InlineData("private FixedTimeEquals", "    private static bool FixedTimeEquals(string a, string b)", 1)]
    [InlineData("private FixedTimeEquals", "    return CryptographicOperations.FixedTimeEquals(a, b);", 0)]
    [InlineData("GetAwaiter().GetResult()", "var value = task\n    .GetAwaiter()\n    .GetResult();", 1)]
    [InlineData("GetAwaiter().GetResult()", "/* task.GetAwaiter().GetResult() */ var value = await task;", 0)]
    [InlineData("Untracked Task.Run", "_ = Task.Run(() => WriteAsync());", 1)]
    [InlineData("Untracked Task.Run", "_ = Task\n    .Run(() => WriteAsync());", 1)]
    [InlineData("Untracked Task.Run", "subscriber.Subscribe(channel, (_, value) => Task.Run(() => handler(value)));", 1)]
    [InlineData("Untracked Task.Run", "await Task.Run(() => Compute());", 0)]
    [InlineData("Untracked Task.Run", "var work = Task.Run(() => Compute());", 0)]
    [InlineData("Untracked Task.Run", "// _ = Task.Run(() => WriteAsync());", 0)]
    [InlineData("data protector outside Core", "var protector = _dataProtectionProvider.CreateProtector(Purpose);", 1)]
    [InlineData("data protector outside Core", "var protector = provider\n    .CreateProtector(\"A\")\n    .CreateProtector(\"B\");", 2)]
    [InlineData("data protector outside Core", "settings.ApiKey = settings.ApiKey.ProtectSetting(_dataProtectionProvider, Purpose);", 0)]
    [InlineData("RemoteIpAddress?.ToString()", "var key = httpContext.Connection\n    .RemoteIpAddress?\n    .ToString();", 1)]
    public void Count_MatchesDeclarationsAndCallsButNotComments(string patternName, string code, int expected)
    {
        // Arrange
        var pattern = _patterns.Single(candidate => candidate.Name == patternName);

        // Act
        var actual = pattern.Count(CSharpSourceMask.Comments(code));

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

        foreach (var file in SourceTreeFiles.Enumerate(Path.Combine(repositoryRoot, "src"), "*.cs", IsExcludedDirectory))
        {
            scanned++;

            var relativePath = Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/');
            var text = File.ReadAllText(file);
            string code = null;

            foreach (var pattern in _patterns)
            {
                if (pattern.ExemptDirectory is not null && relativePath.StartsWith(pattern.ExemptDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (pattern.RequiredFileText is not null && !text.Contains(pattern.RequiredFileText, StringComparison.Ordinal))
                {
                    continue;
                }

                // Comments and XML documentation may name a pattern to warn against it.
                code ??= CSharpSourceMask.Comments(text);

                var count = pattern.Count(code);

                if (count > 0)
                {
                    result[pattern.Name][relativePath] = count;
                }
            }
        }

        Assert.True(scanned > 5000, $"Only {scanned} source files were scanned; the scan no longer matches the repository layout.");

        return result;
    }

    private static bool IsExcludedDirectory(string directory)
        => SourceTreeFiles.IsBuildOutputDirectory(directory) || Path.GetFileName(directory) == "node_modules";

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

        /// <summary>
        /// Counts the copies in a file's code, whose comments are already blanked.
        /// </summary>
        public int Count(string code)
            => _regex.Count(code);
    }
}
