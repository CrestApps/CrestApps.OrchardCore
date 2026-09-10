using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Executes provider-confirmed attended (consultative) transfers as three explicit phases, because a warm
/// transfer cannot be expressed by the single-shot <see cref="IContactCenterVoiceTransferProvider"/> boundary:
/// the initiating agent first speaks privately with the destination agent while the customer is held, then
/// either completes the handoff (the customer is joined to the destination agent and the initiating agent
/// leaves) or cancels it (the destination agent is dropped and the customer returns to the initiating agent).
/// </summary>
public interface IContactCenterVoiceAttendedTransferProvider
{
    /// <summary>
    /// Begins a consult: holds the customer and rings the destination agent into a private conversation with the
    /// initiating agent, leaving the original call fully intact if the destination agent never answers.
    /// </summary>
    /// <param name="request">The attended-transfer request identifying the customer call and destination agent.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider operation result.</returns>
    Task<ContactCenterVoiceProviderResult> BeginConsultAsync(
        ContactCenterVoiceAttendedTransferRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes a consult: resumes the held customer, hands ownership of the conversation to the destination
    /// agent, and releases the initiating agent, so the customer keeps talking to the destination agent.
    /// </summary>
    /// <param name="request">The attended-transfer request identifying the customer call and destination agent.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider operation result.</returns>
    Task<ContactCenterVoiceProviderResult> CompleteConsultAsync(
        ContactCenterVoiceAttendedTransferRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a consult: drops the destination agent and resumes the held customer with the initiating agent,
    /// leaving the original call ownership unchanged.
    /// </summary>
    /// <param name="request">The attended-transfer request identifying the customer call and destination agent.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The provider operation result.</returns>
    Task<ContactCenterVoiceProviderResult> CancelConsultAsync(
        ContactCenterVoiceAttendedTransferRequest request,
        CancellationToken cancellationToken = default);
}
