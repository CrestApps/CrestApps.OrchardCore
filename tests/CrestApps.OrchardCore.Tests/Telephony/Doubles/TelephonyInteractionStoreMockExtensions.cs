using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// Wires a mocked <see cref="ITelephonyInteractionStore"/> so that the retrying update methods behave like the
/// real store: they resolve the matching interaction, run the caller's mutation against it, and return it.
/// </summary>
internal static class TelephonyInteractionStoreMockExtensions
{
    /// <summary>
    /// Configures the retrying update methods to operate over the supplied interactions.
    /// </summary>
    /// <param name="store">The mocked store.</param>
    /// <param name="interactions">The interactions the store is expected to resolve.</param>
    /// <returns>The same mock, so the call can be chained.</returns>
    public static Mock<ITelephonyInteractionStore> SetupRetryingUpdates(
        this Mock<ITelephonyInteractionStore> store,
        params TelephonyInteraction[] interactions)
    {
        store
            .Setup(value => value.UpdateByIdAsync(
                It.IsAny<string>(),
                It.IsAny<Func<TelephonyInteraction, bool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string interactionId, Func<TelephonyInteraction, bool> mutate, CancellationToken _) =>
                Apply(
                    interactions.FirstOrDefault(value =>
                        string.Equals(value.InteractionId, interactionId, StringComparison.Ordinal)),
                    mutate));

        store
            .Setup(value => value.UpdateByProviderCallIdAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Func<TelephonyInteraction, bool>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string providerName, string callId, Func<TelephonyInteraction, bool> mutate, CancellationToken _) =>
                Apply(
                    interactions.FirstOrDefault(value =>
                        string.Equals(value.ProviderName, providerName, StringComparison.Ordinal) &&
                        string.Equals(value.CallId, callId, StringComparison.Ordinal)),
                    mutate));

        return store;
    }

    private static TelephonyInteraction Apply(TelephonyInteraction interaction, Func<TelephonyInteraction, bool> mutate)
    {
        if (interaction is null)
        {
            return null;
        }

        mutate(interaction);

        return interaction;
    }
}
