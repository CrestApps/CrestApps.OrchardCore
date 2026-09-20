using CrestApps.Core.ContactCenter;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Runs the Contact Center's startup checks when a tenant activates.
/// </summary>
/// <remarks>
/// <para>
/// The checks themselves are framework services, because what they decide is the suite's reasoning. When they
/// run is the host's, and in Orchard that is tenant activation. A host without tenants runs the same checks
/// from a hosted service instead.
/// </para>
/// <para>
/// A check that throws is logged and the others still run. Activation is not failed over it: taking a tenant
/// down removes the administration screens an operator would use to fix whatever the check complained about,
/// and the checks record their verdicts where a health check can report them.
/// </para>
/// </remarks>
internal sealed class ContactCenterStartupCheckTenantEvents : ModularTenantEvents
{
    private readonly IEnumerable<IContactCenterStartupCheck> _checks;
    private readonly ShellSettings _shellSettings;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterStartupCheckTenantEvents"/> class.
    /// </summary>
    /// <param name="checks">The registered startup checks.</param>
    /// <param name="shellSettings">The tenant shell settings, used to name the tenant in diagnostics.</param>
    /// <param name="logger">The logger.</param>
    public ContactCenterStartupCheckTenantEvents(
        IEnumerable<IContactCenterStartupCheck> checks,
        ShellSettings shellSettings,
        ILogger<ContactCenterStartupCheckTenantEvents> logger)
    {
        _checks = checks;
        _shellSettings = shellSettings;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override async Task ActivatedAsync()
    {
        foreach (var check in _checks)
        {
            try
            {
                await check.ValidateAsync();
            }
            catch (Exception exception)
            {
                // The check could not run, which is a different thing from the check reporting a problem, and
                // is worth saying so plainly.
                _logger.LogError(
                    exception,
                    "The Contact Center startup check '{CheckName}' could not run for tenant '{TenantName}'.",
                    check.Name,
                    _shellSettings.Name);
            }
        }
    }
}
