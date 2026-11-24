namespace CrestApps.OrchardCore.Users;

/// <summary>
/// Tells what uses an audience, so an audience something depends on is not deleted from under it. Register one with
/// <c>services.AddUserAudienceUsage&lt;T&gt;()</c>.
/// </summary>
public interface IUserAudienceUsage
{
    /// <summary>
    /// Lists what uses an audience, by a name an administrator recognizes, such as a required learning rule.
    /// </summary>
    /// <param name="audienceName">The audience.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The names of what uses it; empty when nothing does.</returns>
    Task<IReadOnlyList<string>> ListUsesAsync(string audienceName, CancellationToken cancellationToken = default);
}
