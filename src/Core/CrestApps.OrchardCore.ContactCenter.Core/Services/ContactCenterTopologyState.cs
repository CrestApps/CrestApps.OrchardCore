using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Holds the topology verdict for this tenant so the readiness probe and the work admission gate answer from
/// one decision rather than re-deriving it.
/// </summary>
/// <remarks>
/// Registered as a singleton per tenant shell. The verdict is established during activation and is immutable
/// for the life of the shell, because every input to it — declared profile, database provider, enabled
/// features, resolved lock — can only change by rebuilding the shell.
/// <para>
/// Until the verdict is recorded the tenant is <em>not</em> admissible. Starting admissible and tightening
/// later would open a window in which work is accepted by a deployment that is about to be found unsupported,
/// and that window is exactly when a shell reload is in progress.
/// </para>
/// </remarks>
public sealed class ContactCenterTopologyState
{
    private ContactCenterTopologyValidationResult _result;

    /// <summary>
    /// Gets the recorded verdict, or <see langword="null"/> when validation has not run yet.
    /// </summary>
    public ContactCenterTopologyValidationResult Result => Volatile.Read(ref _result);

    /// <summary>
    /// Gets a value indicating whether this tenant may admit Contact Center work.
    /// </summary>
    public bool IsAdmissible
    {
        get
        {
            var result = Volatile.Read(ref _result);

            return result is not null && result.IsSatisfied;
        }
    }

    /// <summary>
    /// Records the verdict for this tenant shell.
    /// </summary>
    /// <param name="result">The verdict produced by <see cref="ContactCenterTopologyEvaluator"/>.</param>
    public void Record(ContactCenterTopologyValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Volatile.Write(ref _result, result);
    }
}
