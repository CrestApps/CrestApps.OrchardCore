using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Every coordination timing is a lock lease or a wait. A value that is zero, negative, or shorter than the wait
/// it bounds does not degrade gracefully: it either never acquires the lock at all, or lets a second node take
/// work the first is still doing. Failing at startup with the key named is how an operator learns that, rather
/// than a call being routed twice in production.
/// </summary>
public sealed class ContactCenterCoordinationOptionsValidatorTests
{
    [Fact]
    public void Validate_WithTheShippedDefaults_Succeeds()
    {
        var result = new ContactCenterCoordinationOptionsValidator().Validate(null, new ContactCenterCoordinationOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(nameof(ContactCenterCoordinationOptions.InboundLockTimeout))]
    [InlineData(nameof(ContactCenterCoordinationOptions.AssignmentLockTimeout))]
    [InlineData(nameof(ContactCenterCoordinationOptions.ReservationLockTimeout))]
    [InlineData(nameof(ContactCenterCoordinationOptions.ReclaimLockWait))]
    [InlineData(nameof(ContactCenterCoordinationOptions.QueuedWorkSyncLease))]
    [InlineData(nameof(ContactCenterCoordinationOptions.QueuedWorkSyncFallbackInterval))]
    public void Validate_WhenAWaitIsZero_FailsNamingTheKey(string propertyName)
    {
        var options = new ContactCenterCoordinationOptions();
        typeof(ContactCenterCoordinationOptions).GetProperty(propertyName).SetValue(options, TimeSpan.Zero);

        var result = new ContactCenterCoordinationOptionsValidator().Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, failure => failure.Contains(propertyName, StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WhenAReclaimWaitIsZero_Fails()
    {
        // The distributed Redis lock provider gates its acquisition loop on a timeout-derived cancellation
        // token, so a zero wait cancels before the first attempt and the lock is never tried at all.
        var options = new ContactCenterCoordinationOptions { ReclaimLockWait = TimeSpan.Zero };

        var result = new ContactCenterCoordinationOptionsValidator().Validate(null, options);

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData(nameof(ContactCenterCoordinationOptions.AssignmentLockExpiration), nameof(ContactCenterCoordinationOptions.AssignmentLockTimeout))]
    [InlineData(nameof(ContactCenterCoordinationOptions.ReservationLockExpiration), nameof(ContactCenterCoordinationOptions.ReservationLockTimeout))]
    [InlineData(nameof(ContactCenterCoordinationOptions.InboundLockExpiration), nameof(ContactCenterCoordinationOptions.InboundLockTimeout))]
    public void Validate_WhenALeaseDoesNotExceedItsWait_Fails(string leaseName, string waitName)
    {
        // A lease that expires while a peer is still waiting for it lets two nodes act on the same work.
        var options = new ContactCenterCoordinationOptions();
        var type = typeof(ContactCenterCoordinationOptions);

        type.GetProperty(waitName).SetValue(options, TimeSpan.FromSeconds(30));
        type.GetProperty(leaseName).SetValue(options, TimeSpan.FromSeconds(10));

        var result = new ContactCenterCoordinationOptionsValidator().Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures, failure => failure.Contains(leaseName, StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WhenTheExpiryPageIsZero_Fails()
    {
        // A zero page drains nothing, so expired reservations would accumulate while the pass reports success.
        var options = new ContactCenterCoordinationOptions { ExpiryPageSize = 0 };

        var result = new ContactCenterCoordinationOptionsValidator().Validate(null, options);

        Assert.False(result.Succeeded);
    }
}
