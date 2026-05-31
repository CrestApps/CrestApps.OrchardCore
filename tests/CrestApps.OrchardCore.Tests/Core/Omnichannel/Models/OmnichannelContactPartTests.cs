using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Tests.Core.Omnichannel.Models;

public sealed class OmnichannelContactPartTests
{
    [Theory]
    [InlineData("Call")]
    [InlineData("Email")]
    [InlineData("Sms")]
    [InlineData("Chat")]
    public void SetPreference_WhenEnabledFirstTime_ShouldSetFlagAndTimestamp(string preference)
    {
        // Arrange
        var part = new OmnichannelContactPart();
        var utcNow = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        // Act
        SetPreference(part, preference, true, utcNow);

        // Assert
        Assert.True(GetPreferenceValue(part, preference));
        Assert.Equal(utcNow, GetPreferenceTimestamp(part, preference));
    }

    [Theory]
    [InlineData("Call")]
    [InlineData("Email")]
    [InlineData("Sms")]
    [InlineData("Chat")]
    public void SetPreference_WhenAlreadyEnabled_ShouldPreserveOriginalTimestamp(string preference)
    {
        // Arrange
        var part = new OmnichannelContactPart();
        var originalUtc = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var laterUtc = originalUtc.AddDays(1);
        SetPreference(part, preference, true, originalUtc);

        // Act
        SetPreference(part, preference, true, laterUtc);

        // Assert
        Assert.True(GetPreferenceValue(part, preference));
        Assert.Equal(originalUtc, GetPreferenceTimestamp(part, preference));
    }

    [Theory]
    [InlineData("Call")]
    [InlineData("Email")]
    [InlineData("Sms")]
    [InlineData("Chat")]
    public void SetPreference_WhenDisabled_ShouldClearFlagAndTimestamp(string preference)
    {
        // Arrange
        var part = new OmnichannelContactPart();
        var utcNow = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        SetPreference(part, preference, true, utcNow);

        // Act
        SetPreference(part, preference, false, utcNow.AddDays(1));

        // Assert
        Assert.False(GetPreferenceValue(part, preference));
        Assert.Null(GetPreferenceTimestamp(part, preference));
    }

    private static void SetPreference(OmnichannelContactPart part, string preference, bool value, DateTime utcNow)
    {
        switch (preference)
        {
            case "Call":
                part.SetDoNotCall(value, utcNow);
                break;
            case "Email":
                part.SetDoNotEmail(value, utcNow);
                break;
            case "Sms":
                part.SetDoNotSms(value, utcNow);
                break;
            case "Chat":
                part.SetDoNotChat(value, utcNow);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(preference), preference, null);
        }
    }

    private static bool GetPreferenceValue(OmnichannelContactPart part, string preference)
    {
        return preference switch
        {
            "Call" => part.DoNotCall,
            "Email" => part.DoNotEmail,
            "Sms" => part.DoNotSms,
            "Chat" => part.DoNotChat,
            _ => throw new ArgumentOutOfRangeException(nameof(preference), preference, null),
        };
    }

    private static DateTime? GetPreferenceTimestamp(OmnichannelContactPart part, string preference)
    {
        return preference switch
        {
            "Call" => part.DoNotCallUtc,
            "Email" => part.DoNotEmailUtc,
            "Sms" => part.DoNotSmsUtc,
            "Chat" => part.DoNotChatUtc,
            _ => throw new ArgumentOutOfRangeException(nameof(preference), preference, null),
        };
    }
}
