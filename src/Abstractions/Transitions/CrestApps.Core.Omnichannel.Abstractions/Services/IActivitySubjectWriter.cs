using CrestApps.Core.Omnichannel.Models;

namespace CrestApps.Core.Omnichannel.Services;

/// <summary>
/// Reads and writes the subject an activity is about.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="CrestApps.Core.Omnichannel.Services.IOmnichannelSubjectAccessor"/>
/// because an activity's subject has no identifier to look it up by: it is carried on the activity
/// itself, and may not exist until something writes to it.
/// </para>
/// <para>
/// This exists so the services that record what an automated conversation learned do not have to know
/// what the subject is stored as. The host still stores it exactly as it always did; only the reach
/// into that shape moved behind this contract.
/// </para>
/// </remarks>
public interface IActivitySubjectWriter
{
    /// <summary>
    /// Reads the subject's current field values.
    /// </summary>
    /// <remarks>
    /// Used to show a model what is already on record, so it is asked to fill gaps rather than to
    /// re-state what is known.
    /// </remarks>
    /// <param name="activity">The activity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The values, keyed by field name. Empty when the activity has no subject yet.</returns>
    Task<IReadOnlyDictionary<string, string>> ReadAsync(OmnichannelActivity activity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes values onto the activity's subject, creating one when there is none.
    /// </summary>
    /// <remarks>
    /// Only keys naming a field the subject definition declares are applied, so a model can never
    /// author structure the tenant did not ask for. The subject is left on the activity; committing
    /// the activity is the caller's to do, because it decides when that is safe.
    /// </remarks>
    /// <param name="activity">The activity, whose subject is updated in place.</param>
    /// <param name="fieldValues">The values to write, keyed by field name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the subject changed.</returns>
    Task<bool> ApplyAsync(
        OmnichannelActivity activity,
        IReadOnlyDictionary<string, string> fieldValues,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the fields a conversation may set on this activity's subject.
    /// </summary>
    /// <param name="activity">The activity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The field names. Empty when the activity names no kind of subject.</returns>
    Task<IReadOnlyCollection<string>> GetWritableFieldNamesAsync(OmnichannelActivity activity, CancellationToken cancellationToken = default);
}
