using CrestApps.OrchardCore.Accounts.Core.Services;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.PersonalData;

namespace CrestApps.OrchardCore.Accounts.Services;

/// <summary>
/// Exports the account invitations sent to a person's e-mail addresses or accepted by them, and deletes them.
/// </summary>
internal sealed class AccountInvitationsPersonalDataContributor : IPersonalDataContributor
{
    private const string Kind = "AccountInvitation";

    private readonly IAccountInvitationService _invitations;
    private readonly AccountInvitationStore _store;

    public AccountInvitationsPersonalDataContributor(IAccountInvitationService invitations, AccountInvitationStore store)
    {
        _invitations = invitations;
        _store = store;
    }

    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        foreach (var invitation in await _invitations.ListForPeopleAsync(context.Subject.Emails, context.Subject.UserIds, cancellationToken))
        {
            var record = context.Add(Kind, invitation.ItemId, invitation.Email, invitation.InvitedUtc);

            record.Fields["Account"] = invitation.AccountId;
            record.Fields["Email"] = invitation.Email;
            record.Fields["Role"] = invitation.Role.ToString();
            record.Fields["State"] = invitation.State.ToString();
            record.Fields["InvitedBy"] = invitation.InvitedBy;
        }
    }

    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        foreach (var invitation in await _invitations.ListForPeopleAsync(context.Subject.Emails, context.Subject.UserIds, cancellationToken))
        {
            await _store.DeleteAsync(invitation, cancellationToken);
            context.Result.Count(Kind);
        }
    }
}
