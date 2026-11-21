using System.Globalization;
using CrestApps.OrchardCore.Throttling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.Core.Throttling;

/// <summary>
/// Applies the tenant's <c>CrestApps_Throttling:AttemptPolicies</c> section after the code registrations, so an operator
/// can change any policy without a deployment, whichever module registered it.
/// </summary>
internal sealed class AttemptPolicyConfiguration : IPostConfigureOptions<AttemptLimitOptions>
{
    private readonly IShellConfiguration _shellConfiguration;

    // A container outside a tenant, such as a test's, has no shell configuration and so no overrides.
    public AttemptPolicyConfiguration(IEnumerable<IShellConfiguration> shellConfigurations)
    {
        _shellConfiguration = shellConfigurations.FirstOrDefault();
    }

    public void PostConfigure(string name, AttemptLimitOptions options)
    {
        if (_shellConfiguration is not null)
        {
            Apply(_shellConfiguration.GetSection(AttemptLimitOptions.SectionName), options);
        }
    }

    // Values are read with the invariant culture, so "00:10:00" means the same on every server, and a window of zero or
    // less, which no counter can use, keeps the policy's own.
    internal static void Apply(IConfiguration section, AttemptLimitOptions options)
    {
        foreach (var child in section.GetChildren())
        {
            var policy = options.Policies.TryGetValue(child.Key, out var existing)
                ? existing
                : options.AddPolicy(child.Key, 0, TimeSpan.FromMinutes(1)).Policies[child.Key];

            if (int.TryParse(child[nameof(AttemptLimitPolicy.PermitLimit)], NumberStyles.Integer, CultureInfo.InvariantCulture, out var permitLimit))
            {
                policy.PermitLimit = permitLimit;
            }

            if (TimeSpan.TryParse(child[nameof(AttemptLimitPolicy.Window)], CultureInfo.InvariantCulture, out var window) && window > TimeSpan.Zero)
            {
                policy.Window = window;
            }

            if (TimeSpan.TryParse(child[nameof(AttemptLimitPolicy.BlockDuration)], CultureInfo.InvariantCulture, out var blockDuration) && blockDuration >= TimeSpan.Zero)
            {
                policy.BlockDuration = blockDuration;
            }

            if (bool.TryParse(child[nameof(AttemptLimitPolicy.ResetOnSuccess)], out var resetOnSuccess))
            {
                policy.ResetOnSuccess = resetOnSuccess;
            }
        }
    }
}