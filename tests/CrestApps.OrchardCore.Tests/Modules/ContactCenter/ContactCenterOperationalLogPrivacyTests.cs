using System.Reflection;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterOperationalLogPrivacyTests
{
    [Theory]
    [InlineData("+15551234567")]
    [InlineData("agent-user-123")]
    [InlineData("secret-token-456")]
    public void SanitizeLogValue_WhenValueIsSensitive_PreservesRawValueToday(string sensitiveValue)
    {
        // Act
        var sanitized = sensitiveValue.SanitizeLogValue();

        // Assert
        Assert.Equal(sensitiveValue, sanitized);
    }

    [Fact]
    public void SanitizeLogValue_WhenValueContainsControlCharacters_RemovesControlsButPreservesSensitiveContent()
    {
        // Act
        var sanitized = "agent-user-123\r\n+15551234567".SanitizeLogValue();

        // Assert
        Assert.DoesNotContain('\r', sanitized);
        Assert.DoesNotContain('\n', sanitized);
        Assert.Contains("agent-user-123", sanitized, StringComparison.Ordinal);
        Assert.Contains("+15551234567", sanitized, StringComparison.Ordinal);
    }

    [Fact]
    public void TelephonyHub_RequestDescription_IncludesRawCustomerAddressesToday()
    {
        // Arrange
        var method = typeof(TelephonyHub).GetMethod(
            "DescribeDialRequest",
            BindingFlags.NonPublic | BindingFlags.Static);
        var request = new DialRequest
        {
            To = "+15551234567",
            From = "+15557654321",
        };

        // Act
        var description = Assert.IsType<string>(method?.Invoke(null, [request]));

        // Assert
        Assert.Contains(request.To, description, StringComparison.Ordinal);
        Assert.Contains(request.From, description, StringComparison.Ordinal);
    }

    [Fact]
    public void SensitiveLoggingPaths_UseRawCustomerAndAgentIdentifiersToday()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var telephonyHub = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CrestApps.OrchardCore.Telephony",
            "Hubs",
            "TelephonyHub.cs"));
        var smsHandler = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CrestApps.OrchardCore.Omnichannel.Sms",
            "Handlers",
            "SmsOmnichannelEventHandler.cs"));
        var presenceManager = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "src",
                "Core",
                "CrestApps.OrchardCore.ContactCenter.Core",
                "Services",
                "AgentPresenceManagerService.cs"))
            .ReplaceLineEndings("\n");

        // Assert
        Assert.Contains(
            "Telephony hub action {Action} completed for user {UserId}. Request: {Request}.",
            telephonyHub,
            StringComparison.Ordinal);
        Assert.Contains(
            "Customer Address: {CustomerAddress}",
            smsHandler,
            StringComparison.Ordinal);
        Assert.Contains(
            "omnichannelEvent.Message.CustomerAddress.SanitizeLogValue()",
            smsHandler,
            StringComparison.Ordinal);
        Assert.Contains(
            "Completed Contact Center sign-in for agent '{AgentId}' and user '{UserId}'",
            presenceManager,
            StringComparison.Ordinal);
        Assert.Contains(
            "profile.ItemId,\n                userId,\n                profile.PresenceStatus",
            presenceManager,
            StringComparison.Ordinal);
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
