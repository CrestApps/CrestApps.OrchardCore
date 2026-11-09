namespace CrestApps.OrchardCore.Outreach;

/// <summary>
/// The constants of the outreach engine.
/// </summary>
public static class OutreachConstants
{
    /// <summary>
    /// The feature that stores outreach runs and sends their batches.
    /// </summary>
    public const string Feature = "CrestApps.OrchardCore.Outreach";

    /// <summary>
    /// The YesSql collection outreach runs and their batches live in.
    /// </summary>
    public const string CollectionName = "Outreach";

    /// <summary>
    /// The name of the template renderer that sends the run's template as it is.
    /// </summary>
    public const string TextRenderer = "Text";

    /// <summary>
    /// The name of the e-mail channel sender.
    /// </summary>
    public const string EmailSender = "Email";

    /// <summary>
    /// The recipients a batch sends to when the run does not say.
    /// </summary>
    public const int DefaultPageSize = 50;

    /// <summary>
    /// The skip reasons the engine records itself.
    /// </summary>
    public static class SkipReasons
    {
        /// <summary>
        /// The recipient has no address on the run's channel.
        /// </summary>
        public const string NoAddress = "NoAddress";

        /// <summary>
        /// The recipient asked not to be contacted on the run's channel.
        /// </summary>
        public const string OptedOut = "OptedOut";

        /// <summary>
        /// The recipient has not agreed to receive this kind of message.
        /// </summary>
        public const string NoConsent = "NoConsent";
    }
}
