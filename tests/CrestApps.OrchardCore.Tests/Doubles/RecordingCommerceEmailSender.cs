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
    {
        if (!CanSend || string.IsNullOrWhiteSpace(email.To))
        {
            return false;
        }

        var content = await buildAsync(cancellationToken);

        if (content is null)
        {
            return false;
        }

        Sent.Add((email, content));

        return true;
    }
}
