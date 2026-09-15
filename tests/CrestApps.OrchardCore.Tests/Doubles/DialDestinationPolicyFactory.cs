using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Core.Services;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// Builds the real dial destination policy for tests. The real policy is used rather than a permissive stub so a
/// test that dials a refused destination fails here as it would in production.
/// </summary>
internal static class DialDestinationPolicyFactory
{
    public static DefaultDialDestinationPolicy Create(params string[] allowedShortCodes)
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
