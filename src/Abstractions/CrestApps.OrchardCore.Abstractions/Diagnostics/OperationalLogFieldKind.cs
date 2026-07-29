namespace CrestApps.OrchardCore.Diagnostics;

/// <summary>
/// Classifies the operational-logging sensitivity of a value so <see cref="OperationalLogRedactor"/> can apply a
/// consistent redaction or pseudonymization rule across every Contact Center, Telephony, and Omnichannel provider path.
/// </summary>
public enum OperationalLogFieldKind
{
    /// <summary>
    /// A stable correlation identifier, such as a user, agent, session, call, interaction, activity, reservation, or
    /// queue id. Identifiers are pseudonymized with a process-local keyed hash so operators can correlate related
    /// log lines within one process lifetime without ever seeing the raw value.
    /// </summary>
    Identifier,

    /// <summary>
    /// A customer or endpoint address, such as an E.164 phone number, email address, or SIP URI. Addresses are never
    /// emitted in logs, not even partially.
    /// </summary>
    Address,

    /// <summary>
    /// A secret, credential, or token-shaped value, such as an API key, password, bearer token, or connection
    /// string. Secrets are never emitted in logs.
    /// </summary>
    Secret,

    /// <summary>
    /// Free-form text that may embed personal data, such as a request description, a metadata payload, or a
    /// provider response or error body. Free text is never emitted in logs verbatim.
    /// </summary>
    FreeText,
}
