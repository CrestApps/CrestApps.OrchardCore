using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Rejects a configuration-backed Asterisk provider that is unusable, and, in production, one that is still
/// carrying a credential published in this repository.
/// </summary>
/// <remarks>
/// The development-credential rules are limited to production because the sample values exist precisely so a
/// developer can run the Aspire stack without inventing their own. Applying the rule everywhere would break the
/// workflow it was written to protect; applying it nowhere would let the same values reach a deployment where
/// they authenticate nothing.
/// </remarks>
public sealed class DefaultAsteriskOptionsValidator : IValidateOptions<DefaultAsteriskOptions>
{
    private readonly IHostEnvironment _hostEnvironment;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAsteriskOptionsValidator"/> class.
    /// </summary>
    /// <param name="hostEnvironment">The host environment, used to decide whether production rules apply.</param>
    public DefaultAsteriskOptionsValidator(IHostEnvironment hostEnvironment)
    {
        _hostEnvironment = hostEnvironment;
    }

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string name, DefaultAsteriskOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // An absent configuration section is the normal state for a deployment that uses the tenant-configured
        // provider instead, and imposes no requirements.
        if (!options.IsEnabled)
        {
            return ValidateOptionsResult.Skip;
        }

        var failures = new List<string>();

        if (options.TimeoutSeconds < 1)
        {
            failures.Add($"'{AsteriskConstants.DefaultConfigurationSectionPath}:TimeoutSeconds' must be at least one second.");
        }

        if (options.PjsipCredentialLifetimeMinutes < 1)
        {
            failures.Add($"'{AsteriskConstants.DefaultConfigurationSectionPath}:PjsipCredentialLifetimeMinutes' must be at least one minute.");
        }

        if (options.PjsipContactExpirationSeconds < 1)
        {
            failures.Add($"'{AsteriskConstants.DefaultConfigurationSectionPath}:PjsipContactExpirationSeconds' must be at least one second.");
        }

        if (options.VoicemailPriority < 1)
        {
            failures.Add($"'{AsteriskConstants.DefaultConfigurationSectionPath}:VoicemailPriority' must be at least one.");
        }

        if (_hostEnvironment.IsProduction())
        {
            AddDevelopmentValueFailure(failures, "Password", options.Password);
            AddDevelopmentValueFailure(failures, "TurnSharedSecret", options.TurnSharedSecret);
            AddDevelopmentValueFailure(failures, "UserName", options.UserName);
            AddDevelopmentValueFailure(failures, "PjsipRealtimeConnectionString", options.PjsipRealtimeConnectionString);
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void AddDevelopmentValueFailure(
        List<string> failures,
        string settingName,
        string value)
    {
        if (!KnownDevelopmentValues.IsDevelopmentValue(value, out var reason))
        {
            return;
        }

        failures.Add(
            $"'{AsteriskConstants.DefaultConfigurationSectionPath}:{settingName}' cannot be used in a production environment because {reason}.");
    }
}
