namespace CrestApps.Core.Telephony.Asterisk.Services;

public interface IAsteriskPjsipDialogTerminator
{
    Task TerminateAsync(
        string authorizationUser,
        string reason,
        CancellationToken cancellationToken = default);
}
