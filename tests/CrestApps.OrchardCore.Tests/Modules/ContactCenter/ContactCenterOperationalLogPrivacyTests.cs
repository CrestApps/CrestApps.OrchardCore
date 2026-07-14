using System.Reflection;
using System.Text.RegularExpressions;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Proves the R1 centralized operational-log redaction contract: no Contact Center, Telephony, Asterisk, DialPad,
/// or Omnichannel.Sms path emits a raw E.164/customer address, a stable personal identifier, or a secret/token, while
/// control-character log-forging protection continues to work.
/// </summary>
public sealed partial class ContactCenterOperationalLogPrivacyTests
{
    private const string SentinelE164 = "+15551234567";
    private const string SentinelSecondaryE164 = "+15557654321";
    private const string SentinelUserId = "agent-user-123";
    private const string SentinelCallId = "call-sentinel-789";
    private const string SentinelSecret = "secret-token-456-0123456789abcdef";

    [Fact]
    public void SanitizeLogValue_WhenValueContainsControlCharacters_StripsControlCharactersForLogForgingProtection()
    {
        // Act
        var sanitized = $"{SentinelUserId}\r\n{SentinelE164}".SanitizeLogValue();

        // Assert
        Assert.DoesNotContain('\r', sanitized);
        Assert.DoesNotContain('\n', sanitized);
    }

    [Fact]
    public void OperationalLogRedactor_WhenGivenSentinelValues_NeverEmitsThemRaw()
    {
        // Act
        var redactedAddress = OperationalLogRedactor.Redact(SentinelE164, OperationalLogFieldKind.Address);
        var redactedSecret = OperationalLogRedactor.Redact(SentinelSecret, OperationalLogFieldKind.Secret);
        var pseudonymizedUserId = OperationalLogRedactor.Pseudonymize(SentinelUserId, OperationalLogIdentifierCategory.User);
        var pseudonymizedCallId = OperationalLogRedactor.Pseudonymize(SentinelCallId, OperationalLogIdentifierCategory.Call);
        var redactedMetadata = OperationalLogRedactor.RedactMetadata(new Dictionary<string, object>
        {
            ["callerId"] = SentinelE164,
            ["apiKey"] = SentinelSecret,
        });

        // Assert
        Assert.DoesNotContain(SentinelE164, redactedAddress, StringComparison.Ordinal);
        Assert.DoesNotContain(SentinelSecret, redactedSecret, StringComparison.Ordinal);
        Assert.DoesNotContain(SentinelUserId, pseudonymizedUserId, StringComparison.Ordinal);
        Assert.DoesNotContain(SentinelCallId, pseudonymizedCallId, StringComparison.Ordinal);
        Assert.DoesNotContain(SentinelE164, redactedMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain(SentinelSecret, redactedMetadata, StringComparison.Ordinal);
    }

    [Fact]
    public void TelephonyHub_DescribeDialRequest_NeverContainsRawCustomerAddresses()
    {
        // Arrange
        var method = typeof(TelephonyHub).GetMethod(
            "DescribeDialRequest",
            BindingFlags.NonPublic | BindingFlags.Static);
        var request = new DialRequest
        {
            To = SentinelE164,
            From = SentinelSecondaryE164,
        };

        // Act
        var description = Assert.IsType<string>(method?.Invoke(null, [request]));

        // Assert
        Assert.DoesNotContain(request.To, description, StringComparison.Ordinal);
        Assert.DoesNotContain(request.From, description, StringComparison.Ordinal);
    }

    [Fact]
    public void TelephonyHub_DescribeCallReference_NeverContainsRawCallIdOrSecretMetadata()
    {
        // Arrange
        var method = typeof(TelephonyHub).GetMethod(
            "DescribeCallReference",
            BindingFlags.NonPublic | BindingFlags.Static);
        var call = new CallReference
        {
            CallId = SentinelCallId,
            Metadata = new Dictionary<string, object>
            {
                ["apiKey"] = SentinelSecret,
                ["conferenceBridgeId"] = "bridge-sentinel-321",
            },
        };

        // Act
        var description = Assert.IsType<string>(method?.Invoke(null, [call]));

        // Assert
        Assert.DoesNotContain(SentinelCallId, description, StringComparison.Ordinal);
        Assert.DoesNotContain(SentinelSecret, description, StringComparison.Ordinal);
        Assert.DoesNotContain("bridge-sentinel-321", description, StringComparison.Ordinal);
        Assert.Contains("id_", description, StringComparison.Ordinal);
    }

    [Fact]
    public void TelephonyHub_DescribeTransferRequest_NeverContainsRawCallIdOrAddress()
    {
        // Arrange
        var method = typeof(TelephonyHub).GetMethod(
            "DescribeTransferRequest",
            BindingFlags.NonPublic | BindingFlags.Static);
        var request = new TransferRequest
        {
            CallId = SentinelCallId,
            To = SentinelE164,
        };

        // Act
        var description = Assert.IsType<string>(method?.Invoke(null, [request]));

        // Assert
        Assert.DoesNotContain(SentinelCallId, description, StringComparison.Ordinal);
        Assert.DoesNotContain(SentinelE164, description, StringComparison.Ordinal);
    }

    [Fact]
    public void SensitiveLoggingPaths_NoLongerEmitRawCustomerOrAgentIdentifiers()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var telephonyHub = ReadSource(repositoryRoot, "Modules", "CrestApps.OrchardCore.Telephony", "Hubs", "TelephonyHub.cs");
        var telephonySynchronization = ReadSource(repositoryRoot, "Modules", "CrestApps.OrchardCore.Telephony", "Services", "TelephonyInteractionSynchronizationService.cs");
        var smsHandler = ReadSource(repositoryRoot, "Modules", "CrestApps.OrchardCore.Omnichannel.Sms", "Handlers", "SmsOmnichannelEventHandler.cs");
        var presenceManager = ReadSource(repositoryRoot, "Core", "CrestApps.OrchardCore.ContactCenter.Core", "Services", "AgentPresenceManagerService.cs");
        var asteriskProvider = ReadSource(repositoryRoot, "Modules", "CrestApps.OrchardCore.Asterisk", "Services", "AsteriskTelephonyProviderBase.cs");
        var asteriskDispatcher = ReadSource(repositoryRoot, "Modules", "CrestApps.OrchardCore.Asterisk", "Services", "AsteriskRealtimeVoiceEventDispatcher.cs");
        var dialPadProvider = ReadSource(repositoryRoot, "Modules", "CrestApps.OrchardCore.DialPad", "Services", "DialPadTelephonyProvider.cs");
        var activityAssignment = ReadSource(repositoryRoot, "Core", "CrestApps.OrchardCore.ContactCenter.Core", "Services", "ActivityAssignmentService.cs");
        var contactCenterOutbox = ReadSource(repositoryRoot, "Core", "CrestApps.OrchardCore.ContactCenter.Core", "Services", "ContactCenterOutbox.cs");
        var providerVoiceEventService = ReadSource(repositoryRoot, "Core", "CrestApps.OrchardCore.ContactCenter.Core", "Services", "ProviderVoiceEventService.cs");

        // Assert: raw user/connection identifiers are no longer interpolated directly into TelephonyHub logs.
        Assert.DoesNotContain("Context.UserIdentifier ?? \"(anonymous)\",", telephonyHub, StringComparison.Ordinal);
        Assert.Contains("OperationalLogRedactor.Pseudonymize(Context.UserIdentifier, OperationalLogIdentifierCategory.User)", telephonyHub, StringComparison.Ordinal);
        Assert.Contains("OperationalLogRedactor.Redact(request.To, OperationalLogFieldKind.Address)", telephonyHub, StringComparison.Ordinal);

        // Assert: the Telephony reconciliation service pseudonymizes interaction/call identifiers and redacts provider errors.
        Assert.Contains("OperationalLogRedactor.Pseudonymize(interaction.InteractionId, OperationalLogIdentifierCategory.Interaction)", telephonySynchronization, StringComparison.Ordinal);
        Assert.Contains("OperationalLogRedactor.Redact(lookup.Error, OperationalLogFieldKind.FreeText)", telephonySynchronization, StringComparison.Ordinal);

        // Assert: the SMS handler redacts customer/service addresses instead of merely sanitizing control characters.
        Assert.DoesNotContain("omnichannelEvent.Message.CustomerAddress.SanitizeLogValue()", smsHandler, StringComparison.Ordinal);
        Assert.Contains("OperationalLogRedactor.Redact(omnichannelEvent.Message.CustomerAddress, OperationalLogFieldKind.Address)", smsHandler, StringComparison.Ordinal);
        Assert.Contains("OperationalLogRedactor.Pseudonymize(activity.ItemId, OperationalLogIdentifierCategory.Activity)", smsHandler, StringComparison.Ordinal);

        // Assert: Contact Center agent presence logs no longer interpolate raw agent/user ids.
        Assert.DoesNotContain(
            "profile.ItemId,\n                userId,\n                profile.PresenceStatus",
            presenceManager.ReplaceLineEndings("\n"),
            StringComparison.Ordinal);
        Assert.Contains("OperationalLogRedactor.Pseudonymize(profile.ItemId, OperationalLogIdentifierCategory.Agent)", presenceManager, StringComparison.Ordinal);
        Assert.Contains("OperationalLogRedactor.Pseudonymize(userId, OperationalLogIdentifierCategory.User)", presenceManager, StringComparison.Ordinal);

        // Assert: the Asterisk provider redacts provider response/error bodies and pseudonymizes call/bridge ids.
        Assert.Contains("OperationalLogRedactor.Redact(responseBody, OperationalLogFieldKind.FreeText)", asteriskProvider, StringComparison.Ordinal);
        Assert.Contains("OperationalLogRedactor.Pseudonymize(callId, OperationalLogIdentifierCategory.Call)", asteriskProvider, StringComparison.Ordinal);
        Assert.Contains("OperationalLogRedactor.Pseudonymize(voiceEvent.CallId, OperationalLogIdentifierCategory.Call)", asteriskDispatcher, StringComparison.Ordinal);

        // Assert: DialPad no longer forwards raw provider exception text into a user/log-facing error message.
        Assert.DoesNotContain("ex.Message].Value", dialPadProvider, StringComparison.Ordinal);
        Assert.Contains("OperationalLogRedactor.Pseudonymize(callId, OperationalLogIdentifierCategory.Call)", dialPadProvider, StringComparison.Ordinal);

        // Assert: Contact Center routing/outbox logs pseudonymize agent/queue/reservation/event identifiers.
        Assert.Contains("OperationalLogRedactor.Pseudonymize(decision.Agent.ItemId, OperationalLogIdentifierCategory.Agent)", activityAssignment, StringComparison.Ordinal);
        Assert.Contains("OperationalLogRedactor.Pseudonymize(reservation.ItemId, OperationalLogIdentifierCategory.Reservation)", activityAssignment, StringComparison.Ordinal);
        Assert.Contains("OperationalLogRedactor.Pseudonymize(queueId, OperationalLogIdentifierCategory.Queue)", activityAssignment, StringComparison.Ordinal);
        Assert.Contains("OperationalLogRedactor.Pseudonymize(message.EventId, OperationalLogIdentifierCategory.Event)", contactCenterOutbox, StringComparison.Ordinal);
        Assert.Contains("OperationalLogRedactor.Redact(firstError, OperationalLogFieldKind.FreeText)", contactCenterOutbox, StringComparison.Ordinal);
        Assert.Contains("OperationalLogRedactor.Pseudonymize(providerEvent.ProviderCallId, OperationalLogIdentifierCategory.Call)", providerVoiceEventService, StringComparison.Ordinal);
        Assert.Contains("OperationalLogRedactor.Pseudonymize(session.AgentId, OperationalLogIdentifierCategory.Agent)", providerVoiceEventService, StringComparison.Ordinal);
    }

    [Fact]
    public void OperationalLoggingPaths_NeverPassRawExceptionsToLogger()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var sourceRoots = new[]
        {
            Path.Combine(repositoryRoot, "src", "Core", "CrestApps.OrchardCore.ContactCenter.Core"),
            Path.Combine(repositoryRoot, "src", "Modules", "CrestApps.OrchardCore.ContactCenter"),
            Path.Combine(repositoryRoot, "src", "Modules", "CrestApps.OrchardCore.Telephony"),
            Path.Combine(repositoryRoot, "src", "Modules", "CrestApps.OrchardCore.Asterisk"),
            Path.Combine(repositoryRoot, "src", "Modules", "CrestApps.OrchardCore.DialPad"),
            Path.Combine(repositoryRoot, "src", "Modules", "CrestApps.OrchardCore.Omnichannel.Sms"),
        };
        // Act
        var unsafeFiles = sourceRoots
            .SelectMany(sourceRoot => Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
            .Where(file => RawExceptionLogPattern().IsMatch(File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(repositoryRoot, file))
            .ToArray();

        // Assert
        Assert.Empty(unsafeFiles);
    }

    [GeneratedRegex(
        @"Log(?:Error|Warning|Information|Debug|Critical|Trace)\s*\(\s*(?:ex|\w*[Ee]xception)\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex RawExceptionLogPattern();

    private static string ReadSource(string repositoryRoot, string topLevelFolder, string projectFolder, string subFolder, string fileName)
    {
        return File.ReadAllText(Path.Combine(repositoryRoot, "src", topLevelFolder, projectFolder, subFolder, fileName));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new InvalidOperationException("Unable to locate the repository root.");
    }
}
