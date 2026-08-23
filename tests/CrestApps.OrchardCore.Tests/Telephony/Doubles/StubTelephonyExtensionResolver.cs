using CrestApps.OrchardCore.Telephony.Core.Services;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// A test double for <see cref="ITelephonyExtensionResolver"/>. By default every number resolves to
/// not-found; supply a map of number-to-resolution for tests that exercise extension calling.
/// </summary>
public sealed class StubTelephonyExtensionResolver : ITelephonyExtensionResolver
{
    private readonly IReadOnlyDictionary<string, ExtensionResolution> _resolutions;

    public StubTelephonyExtensionResolver()
        : this(new Dictionary<string, ExtensionResolution>())
    {
    }

    public StubTelephonyExtensionResolver(IReadOnlyDictionary<string, ExtensionResolution> resolutions)
    {
        _resolutions = resolutions ?? new Dictionary<string, ExtensionResolution>();
    }

    public Task<ExtensionResolution> ResolveAsync(string number, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(number) &&
            _resolutions.TryGetValue(number.Trim(), out var resolution))
        {
            return Task.FromResult(resolution);
        }

        return Task.FromResult(ExtensionResolution.NotFound(number));
    }
}
