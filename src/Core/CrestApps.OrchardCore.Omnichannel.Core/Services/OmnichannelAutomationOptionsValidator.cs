using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Validates the automated-activity processing tunables. A zero batch or invocation ceiling processes nothing
/// while reporting success, and a zero attempt ceiling retries an activity forever, so both fail at startup with
/// the offending key named rather than presenting as a queue that quietly never drains.
/// </summary>
public sealed class OmnichannelAutomationOptionsValidator : IValidateOptions<OmnichannelAutomationOptions>
{
    private const string Section = "CrestApps:Omnichannel:Automation";

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string name, OmnichannelAutomationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        RequirePositive(failures, options.ProcessorLeaseMilliseconds, nameof(options.ProcessorLeaseMilliseconds));
        RequirePositive(failures, options.ProcessorBatchSize, nameof(options.ProcessorBatchSize));
        RequirePositive(failures, options.MaxActivitiesPerInvocation, nameof(options.MaxActivitiesPerInvocation));
        RequirePositive(failures, options.MaxProcessingAttempts, nameof(options.MaxProcessingAttempts));
        RequirePositive(failures, options.RetryDelayMinutes, nameof(options.RetryDelayMinutes));

        if (options.ProcessorBatchSize > options.MaxActivitiesPerInvocation)
        {
            failures.Add(
                $"'{Section}:{nameof(options.ProcessorBatchSize)}' cannot exceed '{nameof(options.MaxActivitiesPerInvocation)}', otherwise one batch already overruns the ceiling meant to bound the pass.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void RequirePositive(List<string> failures, int value, string name)
    {
        if (value <= 0)
        {
            failures.Add($"'{Section}:{name}' must be greater than zero.");
        }
    }
}
