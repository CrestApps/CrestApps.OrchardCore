namespace CrestApps.OrchardCore.ContactCenter;

public static partial class ContactCenterConstants
{
    /// <summary>
    /// Contains stable provider-result metadata keys describing a captured recording, shared between voice providers
    /// and the recording service that persists them onto the interaction.
    /// </summary>
    public static class RecordingMetadata
    {
        /// <summary>
        /// Identifies the recording as the provider itself names it, used as the retrieval reference when the
        /// provider reports no durable storage reference.
        /// </summary>
        public const string ProviderRecordingId = "providerRecordingId";

        /// <summary>
        /// Identifies the durable storage reference used to retrieve the recording media.
        /// </summary>
        public const string StorageReference = "storageReference";

        /// <summary>
        /// Identifies the recording media format.
        /// </summary>
        public const string Format = "format";

        /// <summary>
        /// Identifies the recording duration, in seconds, when the provider reports it.
        /// </summary>
        public const string DurationSeconds = "durationSeconds";

        /// <summary>
        /// Identifies the provider-relative path used to retrieve the stored recording.
        /// </summary>
        public const string RetrievalPath = "retrievalPath";
    }

    /// <summary>
    /// Contains stable machine-readable reason codes describing why a recording governance policy denied recording,
    /// shared between the governance policy and the recording service that records them on denial events.
    /// </summary>
    public static class RecordingGovernanceDenyReason
    {
        /// <summary>
        /// Recording is disabled for the tenant by the recording governance policy.
        /// </summary>
        public const string RecordingDisabled = "recordingDisabled";

        /// <summary>
        /// Recording requires explicit party consent that has not been captured on the interaction.
        /// </summary>
        public const string ConsentRequired = "consentRequired";
    }

    /// <summary>
    /// Contains stable machine-readable reason codes describing why a recording governance policy denied a
    /// right-to-erasure request, shared between the erasure service and the denial events it publishes.
    /// </summary>
    public static class RecordingErasureDenyReason
    {
        /// <summary>
        /// The interaction has no captured recording reference to erase.
        /// </summary>
        public const string NoRecording = "noRecording";

        /// <summary>
        /// The recording is under legal hold and is exempt from erasure until the hold is released.
        /// </summary>
        public const string LegalHold = "legalHold";
    }

    /// <summary>
    /// Contains stable machine-readable reason codes describing why the recording media is being erased, carried
    /// on the erasure event so downstream media deletion and audit can attribute the cause.
    /// </summary>
    public static class RecordingErasureReason
    {
        /// <summary>
        /// The recording media is being erased because its retention window elapsed.
        /// </summary>
        public const string Retention = "retention";
    }
}
