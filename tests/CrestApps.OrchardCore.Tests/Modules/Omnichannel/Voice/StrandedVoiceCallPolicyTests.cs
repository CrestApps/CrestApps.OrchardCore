using CrestApps.OrchardCore.Omnichannel.Voice.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

/// <summary>
/// When an automated voice call that never reported its end is taken as over.
/// </summary>
public sealed class StrandedVoiceCallPolicyTests
{
    private static readonly DateTime _now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ACallStillTalking_IsNotStranded()
    {
        Assert.False(StrandedVoiceCallPolicy.IsStranded(_now, dialedUtc: _now.AddMinutes(-40), lastTurnUtc: _now.AddSeconds(-20), _now.AddDays(-1), _now.AddDays(-1)));
    }

    [Fact]
    public void ACallQuietSinceItsLastTurn_IsStranded()
    {
        Assert.True(StrandedVoiceCallPolicy.IsStranded(_now, dialedUtc: _now.AddMinutes(-50), lastTurnUtc: _now.AddMinutes(-31), _now.AddDays(-1), _now.AddDays(-1)));
    }

    [Fact]
    public void ACallJustDialed_IsNotStranded_HoweverOldItsActivity()
    {
        // Live, a cloned load kept its original's schedule from hours before and was dialed long after it.
        Assert.False(StrandedVoiceCallPolicy.IsStranded(_now, dialedUtc: _now.AddSeconds(-40), lastTurnUtc: null, _now.AddHours(-6), _now.AddHours(-6)));
    }

    [Fact]
    public void ACallDialedAndNeverHeardFrom_IsStranded()
    {
        Assert.True(StrandedVoiceCallPolicy.IsStranded(_now, dialedUtc: _now.AddMinutes(-31), lastTurnUtc: null, _now.AddHours(-1), _now.AddHours(-1)));
    }

    [Fact]
    public void ACallWithNoDialTime_IsGivenADay()
    {
        Assert.False(StrandedVoiceCallPolicy.IsStranded(_now, dialedUtc: null, lastTurnUtc: null, _now.AddHours(-23), _now.AddHours(-23)));
        Assert.True(StrandedVoiceCallPolicy.IsStranded(_now, dialedUtc: null, lastTurnUtc: null, _now.AddHours(-25), _now.AddHours(-25)));
    }
}
