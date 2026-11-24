using CrestApps.OrchardCore.Learning.Credentials.Services;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.Services;

/// <summary>
/// Holds the awards a rule asks for until the change that triggered them is saved. The award then runs in its own
/// scope, so it reads what was saved, it is idempotent under a lock, and a failure never undoes the learner's change.
/// </summary>
public interface ICredentialAwardQueue
{
    /// <summary>
    /// Asks for an award after the current request.
    /// </summary>
    /// <param name="request">The request.</param>
    void Enqueue(CredentialAwardRequest request);
}
