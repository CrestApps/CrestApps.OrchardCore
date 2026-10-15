namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Records, on an agent's profile, the copy of their voicemail greeting a telephony provider was given while a call
/// was being sent to their voicemail. The copy already exists at the provider when this is called; recording it is
/// bookkeeping that spares the next call an upload, so it is done outside the call command's unit of work, where a
/// write that collides with the agent's live presence cannot fail the command and leave the caller without a
/// greeting.
/// </summary>
public interface IVoicemailGreetingCopyRecorder
{
    /// <summary>
    /// Schedules the copy to be recorded once the current unit of work has completed.
    /// </summary>
    /// <param name="agentId">The identifier of the agent whose greeting was copied.</param>
    /// <param name="providerName">The technical name of the provider that holds the copy.</param>
    /// <param name="mediaReference">The reference the provider plays the greeting by.</param>
    void RecordProviderCopy(string agentId, string providerName, string mediaReference);
}
