using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Services;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Modules.Telephony;

public sealed class DefaultDialDestinationPolicyTests
{
    [Theory]
    [InlineData("911")]
    [InlineData("112")]
    [InlineData("999")]
    [InlineData("000")]
    [InlineData("110")]
    [InlineData("119")]
    [InlineData("100")]
    [InlineData("102")]
    [InlineData("108")]
    [InlineData("113")]
    [InlineData("117")]
    [InlineData("118")]
    [InlineData("122")]
    [InlineData("133")]
    [InlineData("190")]
    [InlineData("191")]
    [InlineData("192")]
    [InlineData("193")]
    [InlineData("194")]
    [InlineData("997")]
    [InlineData("998")]
    public void Evaluate_WhenTheWholeDialedStringIsAnEmergencyCode_Refuses(string address)
    {
        var policy = CreatePolicy();

        var decision = policy.Evaluate(address, new DialDestinationContext());

        Assert.Equal(DialDestinationOutcome.Emergency, decision.Outcome);
        Assert.False(decision.IsAllowed);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    [Theory]
    [InlineData("+15559110911")]
    [InlineData("+441632960112")]
    [InlineData("+15551230999")]
    public void Evaluate_WhenAnE164NumberMerelyEndsInAnEmergencyCode_Allows(string address)
    {
        // The previous suffix match refused every ordinary number whose last three digits happened to look like
        // an emergency code. An emergency code is the whole dialed string, never a tail of a real number.
        var policy = CreatePolicy();

        var decision = policy.Evaluate(address, new DialDestinationContext());

        Assert.Equal(DialDestinationOutcome.Allowed, decision.Outcome);
        Assert.True(decision.IsAllowed);
    }

    [Theory]
    [InlineData("9911")]
    [InlineData("0911")]
    public void Evaluate_WhenAnEmergencyCodeIsDialedBehindATrunkPrefix_Refuses(string address)
    {
        var policy = CreatePolicy();

        var decision = policy.Evaluate(address, new DialDestinationContext { TrunkPrefix = address.Substring(0, 1) });

        Assert.Equal(DialDestinationOutcome.Emergency, decision.Outcome);
    }

    [Theory]
    [InlineData("+19005551234")]
    [InlineData("+19765551234")]
    [InlineData("+447035551234")]
    public void Evaluate_WhenTheNumberIsPremiumRate_Refuses(string address)
    {
        var policy = CreatePolicy();

        var decision = policy.Evaluate(address, new DialDestinationContext());

        Assert.Equal(DialDestinationOutcome.Premium, decision.Outcome);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-number")]
    [InlineData("+1555")]
    public void Evaluate_WhenTheAddressIsNotDialable_ReportsMalformed(string address)
    {
        var policy = CreatePolicy();

        var decision = policy.Evaluate(address, new DialDestinationContext());

        Assert.Equal(DialDestinationOutcome.Malformed, decision.Outcome);
    }

    [Fact]
    public void Evaluate_WhenAShortCodeIsOnTheTenantAllowList_Allows()
    {
        var policy = CreatePolicy(allowedShortCodes: ["611"]);

        var decision = policy.Evaluate("611", new DialDestinationContext());

        Assert.Equal(DialDestinationOutcome.Allowed, decision.Outcome);
    }

    [Fact]
    public void Evaluate_WhenAnEmergencyCodeIsOnTheTenantAllowList_StillRefuses()
    {
        // The allow-list opens short codes such as a carrier's own service number. It must never be able to turn
        // an emergency code into an ordinary destination.
        var policy = CreatePolicy(allowedShortCodes: ["911"]);

        var decision = policy.Evaluate("911", new DialDestinationContext());

        Assert.Equal(DialDestinationOutcome.Emergency, decision.Outcome);
    }

    [Fact]
    public void Evaluate_WhenTheNumberIsAnOrdinaryE164Number_Allows()
    {
        var policy = CreatePolicy();

        var decision = policy.Evaluate("+15551234567", new DialDestinationContext());

        Assert.Equal(DialDestinationOutcome.Allowed, decision.Outcome);
        Assert.True(decision.IsAllowed);
    }

    private static DefaultDialDestinationPolicy CreatePolicy(string[] allowedShortCodes = null)
    {
        var settings = new TelephonySettings
        {
            AllowedShortCodes = allowedShortCodes ?? [],
        };

        return new DefaultDialDestinationPolicy(new StubOptionsSnapshot(settings));
    }

    private sealed class StubOptionsSnapshot : IOptionsSnapshot<TelephonySettings>
    {
        private readonly TelephonySettings _settings;

        public StubOptionsSnapshot(TelephonySettings settings)
        {
            _settings = settings;
        }

        public TelephonySettings Value => _settings;

        public TelephonySettings Get(string name) => _settings;
    }
}
