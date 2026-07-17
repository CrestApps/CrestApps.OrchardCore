namespace CrestApps.OrchardCore.Asterisk.Services;

internal interface IAsteriskPjsipDialogTerminator
{
    Task TerminateAsync(
        string authorizationUser,
        string reason,
        CancellationToken cancellationToken = default);
}
