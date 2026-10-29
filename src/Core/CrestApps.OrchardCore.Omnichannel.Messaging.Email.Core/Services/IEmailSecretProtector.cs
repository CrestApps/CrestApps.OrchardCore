namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;

/// <summary>
/// Protects the secrets the email channel keeps: mailbox passwords and webhook keys. They are stored protected with the
/// tenant's data protection keys and only ever read back in memory, when they are used.
/// </summary>
public interface IEmailSecretProtector
{
    /// <summary>
    /// Protects a secret for storage.
    /// </summary>
    /// <param name="secret">The secret in clear text.</param>
    /// <returns>The protected secret, or <see langword="null"/> when <paramref name="secret"/> is empty.</returns>
    string Protect(string secret);

    /// <summary>
    /// Reads a protected secret back.
    /// </summary>
    /// <param name="protectedSecret">The protected secret.</param>
    /// <returns>The secret in clear text, or <see langword="null"/> when it is empty or can no longer be read (the
    /// keys it was protected with are gone).</returns>
    string Unprotect(string protectedSecret);
}
