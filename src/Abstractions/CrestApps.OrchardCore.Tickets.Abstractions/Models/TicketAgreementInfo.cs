namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// What a ticket needs of an agreement with its account.
/// </summary>
/// <param name="AgreementId">The agreement.</param>
/// <param name="Number">Its number, such as SA-00042.</param>
/// <param name="Name">Its name.</param>
/// <param name="AccountId">The account it is with.</param>
/// <param name="ServiceLevelPolicyId">The service-level policy it brings to tickets, or <see langword="null"/>.</param>
public sealed record TicketAgreementInfo(string AgreementId, string Number, string Name, string AccountId, string ServiceLevelPolicyId);
