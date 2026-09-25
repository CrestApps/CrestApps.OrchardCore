using CrestApps.OrchardCore.ContactCenter;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Names the number Telnyx presents when the contact center calls out, which is a number the tenant owns.
/// </summary>
public sealed class TelnyxOwnNumberSource : IContactCenterOwnNumberSource
{
    private readonly IOptionsMonitor<TelnyxOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxOwnNumberSource"/> class.
    /// </summary>
    /// <param name="options">The Telnyx options.</param>
    public TelnyxOwnNumberSource(IOptionsMonitor<TelnyxOptions> options)
    {
        _options = options;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<string>> GetOwnNumbersAsync(CancellationToken cancellationToken = default)
    {
        var callerId = _options.CurrentValue?.DefaultOutboundCallerId;

        IReadOnlyCollection<string> numbers = string.IsNullOrWhiteSpace(callerId) ? [] : [callerId];

        return Task.FromResult(numbers);
    }
}
