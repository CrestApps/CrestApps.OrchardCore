using System.Reflection;
using System.Text.RegularExpressions;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Proves the centralized operational-log redaction contract now enforced through
/// <c>Microsoft.Extensions.Compliance.Redaction</c>: no Contact Center, Telephony, Asterisk, DialPad, or
/// Omnichannel.Sms path emits a raw E.164/customer address or a stable personal identifier, every sensitive value is
/// wrapped in <c>SanitizeLogValue()</c> or an erasing redactor, control-character
/// log-forging protection continues to work, and exceptions are handed to the logger as the exception parameter instead
/// of being interpolated into the message template.
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
    public void TelephonyHub_DescribeDialRequest_NeverContainsRawCustomerAddresses()
    {
        // Arrange
        var hub = CreateTelephonyHub();
        var request = new DialRequest
        {
            To = SentinelE164,
            From = SentinelSecondaryE164,
        };

        // Act
        var description = InvokeDescribe(hub, "DescribeDialRequest", request);

        // Assert
        Assert.DoesNotContain(request.To, description, StringComparison.Ordinal);
        Assert.DoesNotContain(request.From, description, StringComparison.Ordinal);
    }

    [Fact]
    public void TelephonyHub_DescribeCallReference_NeverContainsRawSecretMetadataValues()
    {
        // Arrange
        var hub = CreateTelephonyHub();
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
        var description = InvokeDescribe(hub, "DescribeCallReference", call);

        // Assert
        Assert.DoesNotContain(SentinelSecret, description, StringComparison.Ordinal);
        Assert.DoesNotContain("bridge-sentinel-321", description, StringComparison.Ordinal);
    }

    [Fact]
    public void TelephonyHub_DescribeTransferRequest_NeverContainsRawAddress()
    {
        // Arrange
        var hub = CreateTelephonyHub();
        var request = new TransferRequest
        {
            CallId = SentinelCallId,
            To = SentinelE164,
        };

        // Act
        var description = InvokeDescribe(hub, "DescribeTransferRequest", request);

        // Assert
        Assert.DoesNotContain(SentinelE164, description, StringComparison.Ordinal);
    }

    [Fact]
    public void LoggingCalls_OnlyEmitSensitiveValuesThroughSanitizationOrRedaction()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();

        // Act
        var violations = FindUnredactedLogArguments(repositoryRoot);

        // Assert
        Assert.True(
            violations.Count == 0,
            $"Every sensitive value logged in the Contact Center, Telephony, Asterisk, DialPad, and Omnichannel.Sms trees must be wrapped in SanitizeLogValue() or an erasing Redactor.Redact(...) call. Unredacted arguments:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    [Fact]
    public void OperationalLoggingPaths_NeverPassRawExceptionsToLogger()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();

        // Act
        var violations = FindMisplacedExceptionArguments(repositoryRoot);

        // Assert
        Assert.True(
            violations.Count == 0,
            $"Exceptions must be passed to the logger as the first (exception) argument and never interpolated into the message template or passed as a format argument. Violations:{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    private static TelephonyHub CreateTelephonyHub()
    {
        var redactorProvider = new ServiceCollection()
            .AddRedaction(builder => builder.SetRedactor<ErasingRedactor>(LogDataClassifications.AddressSet))
            .BuildServiceProvider()
            .GetRequiredService<IRedactorProvider>();

        return new TelephonyHub(
            NullLogger<TelephonyHub>.Instance,
            new PassThroughStringLocalizer<TelephonyHub>(),
            new ShellSettings(),
            redactorProvider);
    }

    private static string InvokeDescribe(TelephonyHub hub, string methodName, object argument)
    {
        var method = typeof(TelephonyHub).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Instance);

        return Assert.IsType<string>(method?.Invoke(hub, [argument]));
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

    private static List<string> FindMisplacedExceptionArguments(string repositoryRoot)
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

                foreach (var exception in FindMisplacedExceptions(invocation))
                {
                    var line = exception.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    violations.Add($"{Path.GetRelativePath(repositoryRoot, file)}({line}): {exception}");
                }
            }
        }

        return violations;
    }

    private static IEnumerable<SyntaxNode> FindMisplacedExceptions(InvocationExpressionSyntax invocation)
    {
        var arguments = invocation.ArgumentList.Arguments;

        for (var index = 0; index < arguments.Count; index++)
        {
            var expression = arguments[index].Expression;

            // The first argument may legitimately be the ILogger exception parameter passed as a bare identifier.
            if (index == 0 && IsExceptionIdentifier(expression))
            {
                continue;
            }

            // A bare exception object passed as a message format argument.
            if (index > 0 && IsExceptionIdentifier(expression))
            {
                yield return expression;

                continue;
            }

            // An exception object interpolated directly into a message template.
            foreach (var interpolation in expression.DescendantNodes().OfType<InterpolationSyntax>())
            {
                if (IsExceptionIdentifier(interpolation.Expression))
                {
                    yield return interpolation.Expression;
                }
            }
        }
    }

    private static bool IsExceptionIdentifier(ExpressionSyntax expression)
        => expression is IdentifierNameSyntax identifier && ExceptionValueName().IsMatch(identifier.Identifier.ValueText);

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

            if (IsInsideSafeWrapper(node, expression))
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

    private static bool IsInsideSafeWrapper(SyntaxNode node, ExpressionSyntax boundary)
    {
        for (var current = node; current is not null && current != boundary.Parent; current = current.Parent)
        {
            if (current is InvocationExpressionSyntax invocation && IsSafeWrapperInvocation(invocation))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSafeWrapperInvocation(InvocationExpressionSyntax invocation)
    {
        var name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.ValueText,
            _ => null,
        };

        return name is "SanitizeLogValue" or "Redact";
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

    [GeneratedRegex(
        "^_?(?:ex|exception|[A-Za-z]+Exception)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ExceptionValueName();

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
