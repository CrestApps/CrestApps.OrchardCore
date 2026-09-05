using CrestApps.OrchardCore.ContactCenter.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The default <see cref="IQueueTreatmentProvider"/> for a tenant whose telephony provider has no treatment
/// support, or that has no voice provider at all. Nothing is played, which is the same silence a caller heard
/// before treatment existed — and importantly it is not an exception thrown at a caller who is already on hold.
/// </summary>
public sealed class NoQueueTreatmentProvider : IQueueTreatmentProvider
{
    /// <inheritdoc/>
    public Task SpeakAsync(string providerCallId, string text, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task StartHoldMusicAsync(string providerCallId, string mediaId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public Task OfferChoiceAsync(string providerCallId, string text, string acceptKey, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
