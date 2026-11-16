namespace CrestApps.OrchardCore.Learning.Credentials.Services;

/// <summary>
/// The error codes of the credential award service.
/// </summary>
public static class CredentialAwardErrors
{
    /// <summary>
    /// The request lacks the definition, the recipient or the rule kind.
    /// </summary>
    public const string InvalidRequest = "InvalidRequest";

    /// <summary>
    /// The definition does not exist or is not published.
    /// </summary>
    public const string DefinitionNotFound = "DefinitionNotFound";

    /// <summary>
    /// The recipient is not a user of the site.
    /// </summary>
    public const string RecipientNotFound = "RecipientNotFound";

    /// <summary>
    /// The recipient has no e-mail address, which the credential names (hashed).
    /// </summary>
    public const string RecipientHasNoEmail = "RecipientHasNoEmail";

    /// <summary>
    /// The site address is unknown, so the credential's addresses cannot be made. Set the site's base URL.
    /// </summary>
    public const string NoIssuerAddress = "NoIssuerAddress";

    /// <summary>
    /// Another request is awarding at the same time; try again.
    /// </summary>
    public const string Busy = "Busy";

    /// <summary>
    /// The award does not exist.
    /// </summary>
    public const string AwardNotFound = "AwardNotFound";

    /// <summary>
    /// The award is already revoked.
    /// </summary>
    public const string AlreadyRevoked = "AlreadyRevoked";

    /// <summary>
    /// A revocation needs a reason.
    /// </summary>
    public const string ReasonRequired = "ReasonRequired";
}
