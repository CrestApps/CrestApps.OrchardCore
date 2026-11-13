using CrestApps.OrchardCore.Commerce.Emails;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// A commerce email sender that builds each email and keeps it, instead of sending it.
/// </summary>
public sealed class RecordingCommerceEmailSender : ICommerceEmailSender
{
    /// <summary>
    /// Gets the emails, with the content each one was built with, in the order they were sent.
    /// </summary>
    public List<(CommerceEmail Email, CommerceEmailContent Content)> Sent { get; } = [];

    /// <inheritdoc />
    public bool CanSend { get; set; } = true;

    /// <inheritdoc />
    public async Task<bool> SendAsync(CommerceEmail email, Func<CancellationToken, Task<CommerceEmailContent>> buildAsync, CancellationToken cancellationToken = default)
        => (await SendWithResultAsync(email, buildAsync, cancellationToken)).Succeeded;

    /// <inheritdoc />
    public async Task<CommerceEmailResult> SendWithResultAsync(CommerceEmail email, Func<CancellationToken, Task<CommerceEmailContent>> buildAsync, CancellationToken cancellationToken = default)
    {
        if (!CanSend)
        {
            return CommerceEmailResult.Failed("The site can't send email.");
        }

        if (string.IsNullOrWhiteSpace(email.To))
        {
            return CommerceEmailResult.Failed("The email has no recipient.");
        }

        var content = await buildAsync(cancellationToken);

        if (content is null)
        {
            return CommerceEmailResult.Failed("There was nothing to send.");
        }

        Sent.Add((email, content));

        return CommerceEmailResult.Success();
    }
}
