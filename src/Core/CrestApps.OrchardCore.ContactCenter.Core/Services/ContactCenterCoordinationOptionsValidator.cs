using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Validates the coordination timings. Every one of them is a lock lease or a wait, and a value that is zero,
/// negative, or shorter than the wait it bounds does not degrade gracefully: it either never acquires the lock
/// or lets a second node take work the first is still doing. Failing at startup with the offending key named is
/// how an operator finds that out, rather than a call being routed twice in production.
/// </summary>
public sealed class ContactCenterCoordinationOptionsValidator : IValidateOptions<ContactCenterCoordinationOptions>
{
    private const string Section = "CrestApps:ContactCenter:Coordination";

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string name, ContactCenterCoordinationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        RequirePositive(failures, options.InboundLockTimeout, nameof(options.InboundLockTimeout));
        RequirePositive(failures, options.InboundLockExpiration, nameof(options.InboundLockExpiration));
        RequirePositive(failures, options.AssignmentLockTimeout, nameof(options.AssignmentLockTimeout));
        RequirePositive(failures, options.AssignmentLockExpiration, nameof(options.AssignmentLockExpiration));
        RequirePositive(failures, options.ReservationLockTimeout, nameof(options.ReservationLockTimeout));
        RequirePositive(failures, options.ReservationLockExpiration, nameof(options.ReservationLockExpiration));
        RequirePositive(failures, options.ReclaimLockWait, nameof(options.ReclaimLockWait));
        RequirePositive(failures, options.QueuedWorkSyncLease, nameof(options.QueuedWorkSyncLease));
        RequirePositive(failures, options.QueuedWorkSyncFallbackInterval, nameof(options.QueuedWorkSyncFallbackInterval));

        RequireLeaseExceedsWait(failures, options.InboundLockExpiration, options.InboundLockTimeout, nameof(options.InboundLockExpiration), nameof(options.InboundLockTimeout));
        RequireLeaseExceedsWait(failures, options.AssignmentLockExpiration, options.AssignmentLockTimeout, nameof(options.AssignmentLockExpiration), nameof(options.AssignmentLockTimeout));
        RequireLeaseExceedsWait(failures, options.ReservationLockExpiration, options.ReservationLockTimeout, nameof(options.ReservationLockExpiration), nameof(options.ReservationLockTimeout));

        if (options.ExpiryPageSize <= 0)
        {
            failures.Add($"'{Section}:{nameof(options.ExpiryPageSize)}' must be greater than zero, otherwise no expired reservation is ever drained.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void RequirePositive(List<string> failures, TimeSpan value, string name)
    {
        if (value <= TimeSpan.Zero)
        {
            failures.Add($"'{Section}:{name}' must be greater than zero.");
        }
    }

    private static void RequireLeaseExceedsWait(
        List<string> failures,
        TimeSpan lease,
        TimeSpan wait,
        string leaseName,
        string waitName)
    {
        if (lease <= wait)
        {
            failures.Add(
                $"'{Section}:{leaseName}' must exceed '{waitName}', otherwise the lease expires while a peer is still waiting for it and two nodes act on the same work.");
        }
    }
}
