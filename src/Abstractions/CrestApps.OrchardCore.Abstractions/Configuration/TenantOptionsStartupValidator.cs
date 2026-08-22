using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Configuration;

/// <summary>
/// Runs every <see cref="OptionsBuilderExtensions.ValidateOnStart{TOptions}"/> registration when a tenant is
/// activated, so an invalid configuration stops the tenant instead of surfacing later as a failure in whatever
/// request first happened to read the option.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="OptionsBuilderExtensions.ValidateOnStart{TOptions}"/> records its validators against
/// <see cref="IStartupValidator"/>, which the generic host invokes once, against the root container, while
/// starting. Orchard builds a separate container per tenant and nothing in the framework invokes
/// <see cref="IStartupValidator"/> for those containers, so a <c>ValidateOnStart</c> call written inside a
/// module's <c>Startup</c> has no effect on its own: the rule only fires when something first resolves the
/// option, which may be minutes into serving traffic or never. This type closes that gap by invoking the
/// validator at the one moment that corresponds to "start" for a tenant.
/// </para>
/// <para>
/// Failure is deliberately fatal to activation. The alternative - recording the fault and continuing - is right
/// for a dependency that may be transiently absent, but wrong for configuration, which is static, is fixed by
/// editing a configuration source rather than through the admin UI, and would otherwise let a deployment run
/// indefinitely on values an operator believes were rejected.
/// </para>
/// </remarks>
public sealed class TenantOptionsStartupValidator : ModularTenantEvents
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ShellSettings _shellSettings;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantOptionsStartupValidator"/> class.
    /// </summary>
    /// <param name="serviceProvider">The tenant service provider the startup validator is resolved from.</param>
    /// <param name="shellSettings">The tenant shell settings, used to name the tenant in the failure log.</param>
    /// <param name="logger">The logger.</param>
    public TenantOptionsStartupValidator(
        IServiceProvider serviceProvider,
        ShellSettings shellSettings,
        ILogger<TenantOptionsStartupValidator> logger)
    {
        _serviceProvider = serviceProvider;
        _shellSettings = shellSettings;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override Task ActivatingAsync()
    {
        var validator = _serviceProvider.GetService<IStartupValidator>();

        // Absent when no module in this tenant registered a ValidateOnStart rule, which is a valid state and
        // not a failure to report.
        if (validator is null)
        {
            return Task.CompletedTask;
        }

        try
        {
            validator.Validate();
        }
        catch (OptionsValidationException ex)
        {
            if (_logger.IsEnabled(LogLevel.Critical))
            {
                _logger.LogCritical(
                    "Tenant '{TenantName}' has an invalid configuration for options '{OptionsType}' and cannot be activated: {Failures}",
                    _shellSettings.Name,
                    ex.OptionsType?.Name ?? "unknown",
                    string.Join(" ", ex.Failures));
            }

            throw;
        }
        catch (AggregateException ex)
        {
            if (_logger.IsEnabled(LogLevel.Critical))
            {
                _logger.LogCritical(
                    "Tenant '{TenantName}' has an invalid configuration and cannot be activated: {Failures}",
                    _shellSettings.Name,
                    string.Join(" ", ex.InnerExceptions.Select(inner => inner.Message)));
            }

            throw;
        }

        return Task.CompletedTask;
    }
}
