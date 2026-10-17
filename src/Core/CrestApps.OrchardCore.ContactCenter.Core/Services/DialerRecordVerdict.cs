using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// What holds a pending campaign record back, if anything, as <see cref="DialerRecordClassifier"/> decides it.
/// </summary>
/// <param name="Blocker">The reason the record is held back, or <see cref="DispatchBlocker.DialableNow"/>.</param>
/// <param name="DueUtc">When the record becomes due, for a wait that ends by itself; otherwise <see langword="null"/>.</param>
/// <param name="LastAttemptUtc">When the record's last call was placed, or <see langword="null"/> when it has had none.</param>
internal readonly record struct DialerRecordVerdict(
    DispatchBlocker Blocker,
    DateTime? DueUtc,
    DateTime? LastAttemptUtc);
