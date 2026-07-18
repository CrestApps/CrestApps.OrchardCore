using CrestApps.OrchardCore.Asterisk.Models;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// The default <see cref="IAsteriskAriApplicationGate"/>. Registered as a shell-generation singleton so a
/// fresh instance—carrying a unique <see cref="_ownershipToken"/>—is created for each Orchard shell
/// generation. Every ARI consumer in that generation resolves the same instance and therefore shares the
/// same token, which reference-counts the generation's ownership claim in the process-wide
/// <see cref="IAsteriskAriApplicationOwnershipRegistry"/> and keeps ownership stable across a shell reload.
/// </summary>
internal sealed class AsteriskAriApplicationGate : IAsteriskAriApplicationGate
{
    private readonly IAsteriskAriApplicationOwnershipRegistry _ownershipRegistry;
    private readonly ShellSettings _shellSettings;
    private readonly DefaultAsteriskOptions _defaultOptions;
    private readonly string _ownershipToken = IdGenerator.GenerateId();

    /// <summary>
    /// Initializes a new instance of the <see cref="AsteriskAriApplicationGate"/> class.
    /// </summary>
    /// <param name="ownershipRegistry">The process-wide registry that tracks ARI application ownership.</param>
    /// <param name="shellSettings">The current tenant shell settings used to scope the host-default check.</param>
    /// <param name="defaultOptions">The configuration-backed default Asterisk options.</param>
    public AsteriskAriApplicationGate(
        IAsteriskAriApplicationOwnershipRegistry ownershipRegistry,
        ShellSettings shellSettings,
        IOptions<DefaultAsteriskOptions> defaultOptions)
    {
        _ownershipRegistry = ownershipRegistry;
        _shellSettings = shellSettings;
        _defaultOptions = defaultOptions.Value;
    }

    /// <inheritdoc/>
    public bool TryAcquire(AsteriskResolvedSettings settings)
    {
        if (settings is null)
        {
            return true;
        }

        if (!_shellSettings.IsDefaultShell() &&
            AsteriskSettingsUtilities.CollidesWithHostDefaultApplication(settings, _defaultOptions))
        {
            return false;
        }

        return _ownershipRegistry.TryClaim(
            settings.BaseUrl,
            settings.ApplicationName,
            _shellSettings.Name,
            _ownershipToken);
    }

    /// <inheritdoc/>
    public bool IsAvailable(AsteriskResolvedSettings settings)
    {
        if (settings is null)
        {
            return true;
        }

        if (!_shellSettings.IsDefaultShell() &&
            AsteriskSettingsUtilities.CollidesWithHostDefaultApplication(settings, _defaultOptions))
        {
            return false;
        }

        return !_ownershipRegistry.IsOwnedByAnotherTenant(
            settings.BaseUrl,
            settings.ApplicationName,
            _shellSettings.Name);
    }

    /// <inheritdoc/>
    public void ReleaseGeneration()
        => _ownershipRegistry.Release(_ownershipToken);
}
