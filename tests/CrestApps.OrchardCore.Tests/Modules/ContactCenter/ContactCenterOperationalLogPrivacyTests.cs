using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
    public void LoggingCalls_OnlyEmitValuesThatWentThroughTheOperationalLogRedactor()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();

        // Act
        var violations = FindUnredactedLogArguments(repositoryRoot);

        // Assert
        Assert.True(
            violations.Count == 0,
            $"Every logged value in the Contact Center, Telephony, Asterisk, DialPad, and Omnichannel.Sms trees must pass through OperationalLogRedactor. Unredacted arguments:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
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

    private static List<string> FindUnredactedLogArguments(string repositoryRoot)
    {
        var violations = new List<string>();

        foreach (var file in EnumerateGuardedSources(repositoryRoot))
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!IsLoggerInvocation(invocation))
                {
                    continue;
                }

                foreach (var argument in invocation.ArgumentList.Arguments)
                {
                    foreach (var name in FindUnredactedSensitiveNames(argument.Expression))
                    {
                        var line = name.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                        violations.Add($"{Path.GetRelativePath(repositoryRoot, file)}({line}): {argument.Expression}");
                    }
                }
            }
        }

        return violations;
    }

    private static IEnumerable<SyntaxNode> FindUnredactedSensitiveNames(ExpressionSyntax expression)
    {
        foreach (var node in expression.DescendantNodesAndSelf())
        {
            var name = node switch
            {
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
                MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.ValueText,
                IdentifierNameSyntax identifier when IsLoggedDirectly(identifier, expression) => identifier.Identifier.ValueText,
                _ => null,
            };

            if (name is null || !SensitiveValueName().IsMatch(name))
            {
                continue;
            }

            if (IsInsideRedactor(node, expression))
            {
                continue;
            }

            yield return node;
        }
    }

    private static bool IsLoggedDirectly(IdentifierNameSyntax identifier, ExpressionSyntax expression)
    {
        if (identifier == expression)
        {
            return true;
        }

        return identifier.Ancestors()
            .TakeWhile(ancestor => ancestor != expression.Parent)
            .Any(ancestor => ancestor is InterpolatedStringExpressionSyntax);
    }

    private static bool IsInsideRedactor(SyntaxNode node, ExpressionSyntax boundary)
    {
        for (var current = node; current is not null && current != boundary.Parent; current = current.Parent)
        {
            if (current is InvocationExpressionSyntax invocation &&
                invocation.Expression.ToString().Contains(nameof(OperationalLogRedactor), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> EnumerateGuardedSources(string repositoryRoot)
    {
        string[] sourceRoots =
        [
            Path.Combine(repositoryRoot, "src", "Core", "CrestApps.OrchardCore.ContactCenter.Core"),
            Path.Combine(repositoryRoot, "src", "Modules", "CrestApps.OrchardCore.ContactCenter"),
            Path.Combine(repositoryRoot, "src", "Modules", "CrestApps.OrchardCore.Telephony"),
            Path.Combine(repositoryRoot, "src", "Modules", "CrestApps.OrchardCore.Asterisk"),
            Path.Combine(repositoryRoot, "src", "Modules", "CrestApps.OrchardCore.DialPad"),
            Path.Combine(repositoryRoot, "src", "Modules", "CrestApps.OrchardCore.Omnichannel.Sms"),
        ];

        return sourceRoots
            .SelectMany(sourceRoot => Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }

    private static bool IsLoggerInvocation(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        return LoggerMethodNames.Contains(memberAccess.Name.Identifier.ValueText) &&
            memberAccess.Expression.ToString().Contains("ogger", StringComparison.Ordinal);
    }

    private static readonly HashSet<string> LoggerMethodNames = new(StringComparer.Ordinal)
    {
        "Log",
        "LogTrace",
        "LogDebug",
        "LogInformation",
        "LogWarning",
        "LogError",
        "LogCritical",
    };

    [GeneratedRegex(
        "^(?:_)?(?:userId|userIdentifier|userName|agentId|agentUserId|stickyAgentUserId|assignedToId|supervisorAgentId|ownerId|contactId|customerId|subscriberId|phoneNumber|phone|e164|customerAddress|serviceAddress|callerId|callerNumber|emailAddress|email|displayName|firstName|lastName|callId|providerCallId|providerLegId|legId|interactionId|providerInteractionId|activityItemId|activityId|reservationId|queueItemId|agentSessionId|itemId|connectionId|sessionId|conversationId|password|secret|apiKey|accessToken|credential|credentials|authorization|signature|responseBody|errorMessage|transcript)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveValueName();

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
