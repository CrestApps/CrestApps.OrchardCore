using CrestApps.OrchardCore.Asterisk.Models;
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
        if (!IsCheckedInDevelopmentValue(value, out var reason))
        {
            return;
        }

        failures.Add(
            $"'{AsteriskConstants.DefaultConfigurationSectionPath}:{settingName}' cannot be used in a production environment because {reason}.");
    }

    private static bool IsCheckedInDevelopmentValue(string value, out string reason)
    {
        reason = null;

        // An absent value is a different failure with a different remedy, and reporting it here would tell an
        // operator to replace a development secret they never configured.
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();

        // Unsubstituted template placeholders left behind when an operator forgets to supply a real value.
        if (IsUnsubstitutedPlaceholder(candidate))
        {
            reason = "the value is an unsubstituted template placeholder";

            return true;
        }

        // The sample credentials checked into this repository's development assets (Aspire AppHost) all carry
        // the 'crestapps-dev' prefix. A value published in the repository authenticates nobody.
        if (candidate.StartsWith("crestapps-dev", StringComparison.OrdinalIgnoreCase))
        {
            reason = "the value is a development credential published in this repository, so it is not secret";

            return true;
        }

        return false;
    }

    private static bool IsUnsubstitutedPlaceholder(string candidate)
    {
        if (candidate.Length < 3)
        {
            return false;
        }

        return (candidate[0] == '<' && candidate[candidate.Length - 1] == '>')
            || (candidate[0] == '[' && candidate[candidate.Length - 1] == ']')
            || (candidate.StartsWith("{{", StringComparison.Ordinal) && candidate.EndsWith("}}", StringComparison.Ordinal))
            || (candidate.StartsWith("__", StringComparison.Ordinal) && candidate.EndsWith("__", StringComparison.Ordinal));
    }
}
